﻿using Aspire.Hosting;
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
