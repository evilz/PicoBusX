using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PicoBusX.Web.Components;
using PicoBusX.Web.Models;

namespace PicoBusX.Web.Tests.Components;

public class EntityDetailsPanelTests : TestContext
{
    public EntityDetailsPanelTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddOptions();
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
}

