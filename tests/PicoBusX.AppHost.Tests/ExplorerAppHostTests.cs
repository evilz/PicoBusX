using Aspire.Hosting;
using Aspire.Hosting.Testing;
using FluentAssertions;

namespace PicoBusX.AppHost.Tests;

public class ExplorerAppHostTests
{
    [Fact(Timeout = 600_000)]
    public async Task AppHost_ExposesHealthEndpoint()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.PicoBusX_AppHost>();

        await using var app = await builder.BuildAsync();
        await app.StartAsync();

        var client = await CreateReadyClientAsync(app);
        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
    }

    [Fact(Timeout = 600_000)]
    public async Task AppHost_ServesExplorerShell()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.PicoBusX_AppHost>();

        await using var app = await builder.BuildAsync();
        await app.StartAsync();

        var client = await CreateReadyClientAsync(app);
        using var response = await client.GetAsync("/", HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }

    private static async Task<HttpClient> CreateReadyClientAsync(DistributedApplication app)
    {
        var client = app.CreateHttpClient("PicoBusX");

        for (var attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                var response = await client.GetAsync("/health");
                if (response.IsSuccessStatusCode)
                {
                    return client;
                }
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException("The PicoBusX web application did not become healthy within the allotted time.");
    }
}
