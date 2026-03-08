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

        ExpandTopic(cut.Instance, "orders-topic");
        cut.Render();

        cut.Markup.Should().Contain("Queues (2)");
        cut.Markup.Should().Contain("Topics (1)");
        cut.Markup.Should().Contain("DL:2");
        cut.Markup.Should().Contain("orders-topic");
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

        ExpandTopic(cut.Instance, "inventory-topic");
        GetPrivateField<BusTreeView>("_filter").SetValue(cut.Instance, "ORDER");
        cut.Render();

        cut.Markup.Should().Contain("Queues (1)");
        cut.Markup.Should().Contain("Topics (1)");
        cut.Markup.Should().Contain("orders");
        cut.Markup.Should().Contain("inventory-topic");
        cut.Markup.Should().NotContain("billing");
        cut.Markup.Should().NotContain("warehouse");
    }

    [Fact]
    public void ClickQueueTreeItem_WhenRendered_InvokesOnEntitySelected()
    {
        BusEntity? captured = null;
        var queue = new QueueInfo { Name = "orders" };

        var cut = RenderComponent<BusTreeView>(parameters => parameters
            .Add(p => p.Queues, [queue])
            .Add(p => p.Topics, [])
            .Add(p => p.OnEntitySelected, EventCallback.Factory.Create<BusEntity>(this, entity => captured = entity)));

        cut.Find("button[role='treeitem']").Click();

        captured.Should().NotBeNull();
        captured!.Type.Should().Be(BusEntityType.Queue);
        captured.Name.Should().Be("orders");
    }

    [Fact]
    public void ClickTopicTreeItem_WhenRendered_ExpandsTopicAndInvokesOnEntitySelected()
    {
        BusEntity? captured = null;
        var topic = new TopicInfo { Name = "orders-topic", Subscriptions = [] };

        var cut = RenderComponent<BusTreeView>(parameters => parameters
            .Add(p => p.Queues, [])
            .Add(p => p.Topics, [topic])
            .Add(p => p.OnEntitySelected, EventCallback.Factory.Create<BusEntity>(this, entity => captured = entity)));

        cut.Find("button.tree-entity-button-expandable").Click();

        captured.Should().NotBeNull();
        captured!.Type.Should().Be(BusEntityType.Topic);
        captured.Name.Should().Be("orders-topic");

        var expandedTopics = GetPrivateField<BusTreeView>("_expandedTopics").GetValue(cut.Instance) as HashSet<string>;
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
            .Add(p => p.OnEntitySelected, EventCallback.Factory.Create<BusEntity>(this, entity => captured = entity)));

        await cut.InvokeAsync(() => InvokePrivateMethod(cut.Instance, "SelectSub", sub));

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
            .Add(p => p.OnEntitySelected, EventCallback.Factory.Create<BusEntity>(this, entity => captured = entity)));

        await cut.InvokeAsync(() => InvokePrivateMethod(cut.Instance, "SelectSub", sub));

        captured!.EntityPath.Should().Be("orders-topic/subscriptions/order-created");
    }

    [Fact]
    public void TreeItems_WithEntityNames_RenderSemanticTreeItems()
    {
        var queue = new QueueInfo { Name = "orders" };
        var topic = new TopicInfo
        {
            Name = "orders-topic",
            Subscriptions =
            [
                new SubscriptionInfo { Name = "order-created", TopicName = "orders-topic" }
            ]
        };

        var cut = RenderComponent<BusTreeView>(parameters => parameters
            .Add(p => p.Queues, [queue])
            .Add(p => p.Topics, [topic]));

        ExpandTopic(cut.Instance, "orders-topic");
        cut.Render();

        cut.Markup.Should().Contain("role=\"tree\"");
        cut.Markup.Should().Contain("role=\"treeitem\"");
        cut.Markup.Should().Contain(">orders<");
        cut.Markup.Should().Contain(">orders-topic<");
    }

    private static void ExpandTopic(BusTreeView instance, string topicName)
    {
        var expandedTopics = GetPrivateField<BusTreeView>("_expandedTopics").GetValue(instance) as HashSet<string>;
        expandedTopics!.Add(topicName);
    }

    private static FieldInfo GetPrivateField<TComponent>(string fieldName) =>
        typeof(TComponent).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static Task InvokePrivateMethod<TComponent>(TComponent instance, string methodName, params object[] args)
    {
        var method = typeof(TComponent).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!;
        var result = method.Invoke(instance, args);
        return result is Task task ? task : Task.CompletedTask;
    }
}
