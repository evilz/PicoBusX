using System.Reflection;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using PicoBusX.Web.Components;
using PicoBusX.Web.Models;

namespace PicoBusX.Web.Tests.Components;

public class BusTreeViewTests : TestContext
{
    public BusTreeViewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
    }

    [Fact]
    public void Tree_RendersGroupCountsAndBadges()
    {
        var cut = RenderComponent<BusTreeView>(parameters => parameters
            .Add(p => p.Queues, new List<QueueInfo>
            {
                new() { Name = "orders", ActiveMessageCount = 5, DeadLetterMessageCount = 2 },
                new() { Name = "billing" }
            })
            .Add(p => p.Topics, new List<TopicInfo>
            {
                new()
                {
                    Name = "orders-topic",
                    Subscriptions =
                    [
                        new SubscriptionInfo { TopicName = "orders-topic", Name = "order-created", ActiveMessageCount = 4, DeadLetterMessageCount = 1 }
                    ]
                }
            }));

        cut.Markup.Should().Contain("Queues (2)");
        cut.Markup.Should().Contain("Topics (1)");
        cut.Markup.Should().Contain("DL:2");
        cut.Markup.Should().Contain("order-created");
    }

    [Fact]
    public void Tree_FilterIsCaseInsensitive_AndPreservesGroupHeaders()
    {
        var cut = RenderComponent<BusTreeView>(parameters => parameters
            .Add(p => p.Queues, new List<QueueInfo>
            {
                new() { Name = "orders" },
                new() { Name = "billing" }
            })
            .Add(p => p.Topics, new List<TopicInfo>
            {
                new()
                {
                    Name = "inventory-topic",
                    Subscriptions =
                    [
                        new SubscriptionInfo { TopicName = "inventory-topic", Name = "order-audit" },
                        new SubscriptionInfo { TopicName = "inventory-topic", Name = "warehouse" }
                    ]
                }
            }));

        GetPrivateField<BusTreeView, string>("_filter").SetValue(cut.Instance, "ORDER");

        cut.Render();

        cut.Markup.Should().Contain("Queues (1)");
        cut.Markup.Should().Contain("Topics (1)");
        cut.Markup.Should().Contain("orders");
        cut.Markup.Should().Contain("order-audit");
        cut.Markup.Should().NotContain("billing");
        cut.Markup.Should().NotContain("warehouse");
    }

    [Fact]
    public async Task SelectQueue_InvokesOnEntitySelectedWithQueueType()
    {
        BusEntity? captured = null;
        var queue = new QueueInfo { Name = "orders" };

        var cut = RenderComponent<BusTreeView>(parameters => parameters
            .Add(p => p.Queues, [queue])
            .Add(p => p.Topics, [])
            .Add(p => p.OnEntitySelected, EventCallback.Factory.Create<BusEntity>(
                this, entity => captured = entity)));

        await cut.InvokeAsync(() => InvokePrivateMethod<BusTreeView>(cut.Instance, "SelectQueue", queue));

        captured.Should().NotBeNull();
        captured!.Type.Should().Be(BusEntityType.Queue);
        captured.Name.Should().Be("orders");
        captured.TopicName.Should().BeNull();
    }

    [Fact]
    public async Task SelectQueue_UpdatesInternalSelectionState()
    {
        var queue = new QueueInfo { Name = "orders" };

        var cut = RenderComponent<BusTreeView>(parameters => parameters
            .Add(p => p.Queues, [queue])
            .Add(p => p.Topics, []));

        await cut.InvokeAsync(() => InvokePrivateMethod<BusTreeView>(cut.Instance, "SelectQueue", queue));

        var selected = GetPrivateField<BusTreeView, BusEntity>("_selected").GetValue(cut.Instance) as BusEntity;
        selected.Should().NotBeNull();
        selected!.Type.Should().Be(BusEntityType.Queue);
        selected.Name.Should().Be("orders");
    }

    [Fact]
    public async Task SelectTopic_InvokesOnEntitySelectedWithTopicTypeAndExpandsTopic()
    {
        BusEntity? captured = null;
        var topic = new TopicInfo { Name = "orders-topic", Subscriptions = [] };

        var cut = RenderComponent<BusTreeView>(parameters => parameters
            .Add(p => p.Queues, [])
            .Add(p => p.Topics, [topic])
            .Add(p => p.OnEntitySelected, EventCallback.Factory.Create<BusEntity>(
                this, entity => captured = entity)));

        await cut.InvokeAsync(() => InvokePrivateMethod<BusTreeView>(cut.Instance, "SelectTopic", topic));

        captured.Should().NotBeNull();
        captured!.Type.Should().Be(BusEntityType.Topic);
        captured.Name.Should().Be("orders-topic");
        captured.TopicName.Should().BeNull();

        var expandedTopics = GetPrivateField<BusTreeView, HashSet<string>>("_expandedTopics")
            .GetValue(cut.Instance) as HashSet<string>;
        expandedTopics.Should().Contain("orders-topic");
    }

    [Fact]
    public async Task SelectSub_InvokesOnEntitySelectedWithSubscriptionTypeAndTopicName()
    {
        BusEntity? captured = null;
        var sub = new SubscriptionInfo { Name = "order-created", TopicName = "orders-topic" };
        var topic = new TopicInfo { Name = "orders-topic", Subscriptions = [sub] };

        var cut = RenderComponent<BusTreeView>(parameters => parameters
            .Add(p => p.Queues, [])
            .Add(p => p.Topics, [topic])
            .Add(p => p.OnEntitySelected, EventCallback.Factory.Create<BusEntity>(
                this, entity => captured = entity)));

        await cut.InvokeAsync(() => InvokePrivateMethod<BusTreeView>(cut.Instance, "SelectSub", sub));

        captured.Should().NotBeNull();
        captured!.Type.Should().Be(BusEntityType.Subscription);
        captured.Name.Should().Be("order-created");
        captured.TopicName.Should().Be("orders-topic");
    }

    [Fact]
    public async Task SelectSub_EntityPathIncludesTopicAndSubscriptionName()
    {
        BusEntity? captured = null;
        var sub = new SubscriptionInfo { Name = "order-created", TopicName = "orders-topic" };

        var cut = RenderComponent<BusTreeView>(parameters => parameters
            .Add(p => p.Queues, [])
            .Add(p => p.Topics, [new TopicInfo { Name = "orders-topic", Subscriptions = [sub] }])
            .Add(p => p.OnEntitySelected, EventCallback.Factory.Create<BusEntity>(
                this, entity => captured = entity)));

        await cut.InvokeAsync(() => InvokePrivateMethod<BusTreeView>(cut.Instance, "SelectSub", sub));

        captured!.EntityPath.Should().Be("orders-topic/subscriptions/order-created");
    }

    private static FieldInfo GetPrivateField<TComponent, TField>(string fieldName) =>
        typeof(TComponent).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static Task InvokePrivateMethod<TComponent>(TComponent instance, string methodName, params object[] args)
    {
        var method = typeof(TComponent).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!;
        var result = method.Invoke(instance, args);
        return result is Task task ? task : Task.CompletedTask;
    }
}
