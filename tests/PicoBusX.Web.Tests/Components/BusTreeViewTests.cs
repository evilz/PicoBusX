using Bunit;
using FluentAssertions;
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

        typeof(BusTreeView)
            .GetField("_filter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(cut.Instance, "ORDER");

        cut.Render();

        cut.Markup.Should().Contain("Queues (1)");
        cut.Markup.Should().Contain("Topics (1)");
        cut.Markup.Should().Contain("orders");
        cut.Markup.Should().Contain("order-audit");
        cut.Markup.Should().NotContain("billing");
        cut.Markup.Should().NotContain("warehouse");
    }
}
