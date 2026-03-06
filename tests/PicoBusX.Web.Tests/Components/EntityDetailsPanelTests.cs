using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using PicoBusX.Web.Components;
using PicoBusX.Web.Models;

namespace PicoBusX.Web.Tests.Components;

public class EntityDetailsPanelTests : TestContext
{
    public EntityDetailsPanelTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddOptions();
        Services.AddFluentUIComponents();
    }

    [Fact]
    public void QueueDetails_RenderNeverAndEmDashForOptionalValues()
    {
        var queue = new QueueInfo
        {
            Name = "orders",
            DefaultMessageTimeToLive = TimeSpan.MaxValue,
            AutoDeleteOnIdle = TimeSpan.FromMinutes(30),
            ForwardTo = null,
            ForwardDeadLetteredMessagesTo = null,
            Status = "Active"
        };

        var cut = RenderComponent<EntityDetailsPanel>(parameters => parameters
            .Add(p => p.Entity, new BusEntity { Type = BusEntityType.Queue, Name = "orders" })
            .Add(p => p.Queues, new List<QueueInfo> { queue })
            .Add(p => p.Topics, new List<TopicInfo>()));

        cut.Markup.Should().Contain("Default Message TTL");
        cut.Markup.Should().Contain("Never");
        cut.Markup.Should().Contain("Forward To");
        cut.Markup.Should().Contain("—");
    }

    [Fact]
    public void QueueDetails_RendersAllExpectedPropertyLabels()
    {
        var queue = new QueueInfo
        {
            Name = "billing",
            ActiveMessageCount = 3,
            DeadLetterMessageCount = 1,
            SizeInBytes = 1024,
            Status = "Active",
            MaxDeliveryCount = 10,
            EnablePartitioning = true,
            EnableBatchedOperations = false
        };

        var cut = RenderComponent<EntityDetailsPanel>(parameters => parameters
            .Add(p => p.Entity, new BusEntity { Type = BusEntityType.Queue, Name = "billing" })
            .Add(p => p.Queues, [queue])
            .Add(p => p.Topics, []));

        cut.Markup.Should().Contain("billing");
        cut.Markup.Should().Contain("Active Messages");
        cut.Markup.Should().Contain("Dead-Letter Messages");
        cut.Markup.Should().Contain("Max Delivery Count");
        cut.Markup.Should().Contain("Enable Partitioning");
        cut.Markup.Should().Contain("Status");
        cut.Markup.Should().Contain("Active");
    }

    [Fact]
    public void QueueEntityNotInList_RendersWarningMessage()
    {
        var cut = RenderComponent<EntityDetailsPanel>(parameters => parameters
            .Add(p => p.Entity, new BusEntity { Type = BusEntityType.Queue, Name = "ghost-queue" })
            .Add(p => p.Queues, [])
            .Add(p => p.Topics, []));

        cut.Markup.Should().Contain("Queue details not available");
    }

    [Fact]
    public void TopicDetails_RenderSubscriptionsSummary()
    {
        var topic = new TopicInfo
        {
            Name = "orders-topic",
            Status = "Active",
            Subscriptions =
            [
                new SubscriptionInfo
                {
                    TopicName = "orders-topic",
                    Name = "order-created",
                    ActiveMessageCount = 3,
                    DeadLetterMessageCount = 1,
                    TransferMessageCount = 0,
                    TransferDeadLetterMessageCount = 0,
                    Status = "Active"
                }
            ]
        };

        var cut = RenderComponent<EntityDetailsPanel>(parameters => parameters
            .Add(p => p.Entity, new BusEntity { Type = BusEntityType.Topic, Name = "orders-topic" })
            .Add(p => p.Queues, new List<QueueInfo>())
            .Add(p => p.Topics, new List<TopicInfo> { topic }));

        cut.Markup.Should().Contain("Subscriptions summary");
        cut.Markup.Should().Contain("order-created");
        cut.Markup.Should().Contain("Active");
    }

    [Fact]
    public void TopicEntityNotInList_RendersWarningMessage()
    {
        var cut = RenderComponent<EntityDetailsPanel>(parameters => parameters
            .Add(p => p.Entity, new BusEntity { Type = BusEntityType.Topic, Name = "ghost-topic" })
            .Add(p => p.Queues, [])
            .Add(p => p.Topics, []));

        cut.Markup.Should().Contain("Topic details not available");
    }

    [Fact]
    public void SubscriptionDetails_RendersTopicAndSubscriptionProperties()
    {
        var sub = new SubscriptionInfo
        {
            TopicName = "orders-topic",
            Name = "order-created",
            ActiveMessageCount = 5,
            DeadLetterMessageCount = 2,
            MaxDeliveryCount = 10,
            RequiresSession = false,
            EnableBatchedOperations = true,
            Status = "Active"
        };

        var topic = new TopicInfo
        {
            Name = "orders-topic",
            Subscriptions = [sub]
        };

        var cut = RenderComponent<EntityDetailsPanel>(parameters => parameters
            .Add(p => p.Entity, new BusEntity
            {
                Type = BusEntityType.Subscription,
                Name = "order-created",
                TopicName = "orders-topic"
            })
            .Add(p => p.Queues, [])
            .Add(p => p.Topics, [topic]));

        cut.Markup.Should().Contain("orders-topic");
        cut.Markup.Should().Contain("order-created");
        cut.Markup.Should().Contain("Active Messages");
        cut.Markup.Should().Contain("Dead-Letter Messages");
        cut.Markup.Should().Contain("Max Delivery Count");
        cut.Markup.Should().Contain("Active");
    }

    [Fact]
    public void SubscriptionEntityNotInList_RendersWarningMessage()
    {
        var cut = RenderComponent<EntityDetailsPanel>(parameters => parameters
            .Add(p => p.Entity, new BusEntity
            {
                Type = BusEntityType.Subscription,
                Name = "ghost-sub",
                TopicName = "orders-topic"
            })
            .Add(p => p.Queues, [])
            .Add(p => p.Topics, []));

        cut.Markup.Should().Contain("Subscription details not available");
    }

    [Fact]
    public void NullEntity_RendersNothing()
    {
        var cut = RenderComponent<EntityDetailsPanel>(parameters => parameters
            .Add(p => p.Entity, null)
            .Add(p => p.Queues, [])
            .Add(p => p.Topics, []));

        cut.Markup.Should().NotContain("details-panel");
        cut.Markup.Trim().Should().BeEmpty();
    }
}

