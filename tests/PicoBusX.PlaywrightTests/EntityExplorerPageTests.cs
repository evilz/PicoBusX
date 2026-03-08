using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using static Microsoft.Playwright.Assertions;

namespace PicoBusX.PlaywrightTests;

public class EntityExplorerPageTests : PageTest
{
    [Fact(Timeout = 600_000)]
    public async Task HomePage_ShowsSeededExplorerGroups()
    {
        await using var app = await StartAppAsync();

        await Page.GotoAsync(GetBaseUrl(app));
        await Expect(Page.GetByText("Queues (3)")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Topics (2)")).ToBeVisibleAsync();
        await Expect(Page.GetByText("orders", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("orders-topic", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Select a queue, topic, or subscription from the tree to get started.")).ToBeVisibleAsync();
    }

    [Fact(Timeout = 600_000)]
    public async Task HomePage_ExposesExplorerControls()
    {
        await using var app = await StartAppAsync();

        await Page.GotoAsync(GetBaseUrl(app));
        await Expect(Page.GetByRole(AriaRole.Searchbox, new() { Name = "Filter entities" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Refresh" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "New Entity" })).ToBeVisibleAsync();
    }

    [Fact(Timeout = 600_000)]
    public async Task HomePage_SelectQueue_ShowsQueueDetailsAndHidesEmptyState()
    {
        await using var app = await StartAppAsync();

        await Page.GotoAsync(GetBaseUrl(app));
        await Expect(Page.GetByText("Select a queue, topic, or subscription from the tree to get started."))
            .ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Treeitem, new() { Name = "orders", Exact = true }).ClickAsync();

        await Expect(Page.GetByText("Select a queue, topic, or subscription from the tree to get started."))
            .ToBeHiddenAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "orders" })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Active Messages")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Details")).ToBeVisibleAsync();
    }

    [Fact(Timeout = 600_000)]
    
    public async Task HomePage_SelectTopic_ShowsTopicDetailsWithSubscriptionsSummary()
    {
        await using var app = await StartAppAsync();

        await Page.GotoAsync(GetBaseUrl(app));

        await Page.GetByRole(AriaRole.Treeitem, new() { Name = "orders-topic", Exact = true }).ClickAsync();

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "orders-topic" })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Subscriptions summary")).ToBeVisibleAsync();
        await Expect(Page.GetByText("order-created")).ToBeVisibleAsync();
        await Expect(Page.GetByText("order-cancelled")).ToBeVisibleAsync();
    }

    [Fact(Timeout = 600_000)]
    public async Task HomePage_SelectSubscription_ShowsSubscriptionDetails()
    {
        await using var app = await StartAppAsync();

        await Page.GotoAsync(GetBaseUrl(app));

        await Page.GetByRole(AriaRole.Treeitem, new() { Name = "orders-topic", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Treeitem, new() { Name = "order-created", Exact = true }).ClickAsync();

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "order-created" })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Active Messages")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Dead-Letter Messages")).ToBeVisibleAsync();
    }

    [Fact(Timeout = 600_000)]
    public async Task HomePage_SwitchingEntities_UpdatesDetailsPanel()
    {
        await using var app = await StartAppAsync();

        await Page.GotoAsync(GetBaseUrl(app));

        await Page.GetByRole(AriaRole.Treeitem, new() { Name = "orders", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "orders" })).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Treeitem, new() { Name = "billing", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "billing" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "orders" })).ToBeHiddenAsync();
    }

    private static async Task<DistributedApplication> StartAppAsync()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.PicoBusX_AppHost>();
        var app = await builder.BuildAsync();
        await app.StartAsync();

        var client = app.CreateHttpClient("PicoBusX");
        for (var attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                var response = await client.GetAsync("/health");
                if (response.IsSuccessStatusCode)
                {
                    return app;
                }
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(1000);
        }

        await app.DisposeAsync();
        throw new TimeoutException("The PicoBusX web application did not become healthy within the allotted time.");
    }

    private static string GetBaseUrl(DistributedApplication app)
    {
        var client = app.CreateHttpClient("PicoBusX");
        return client.BaseAddress?.ToString() ?? throw new InvalidOperationException("The PicoBusX base address is not available.");
    }
}
