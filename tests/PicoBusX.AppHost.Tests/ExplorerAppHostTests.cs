using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Azure.Messaging.ServiceBus;
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

    [Fact(Timeout = 600_000)]
    public async Task ServiceBusEmulator_CanSendAndReceiveSeededQueueMessage()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.PicoBusX_AppHost>();

        await using var app = await builder.BuildAsync();
        await app.StartAsync();
        await app.ResourceNotifications.WaitForResourceHealthyAsync("serviceBus").WaitAsync(TimeSpan.FromMinutes(5));

        var connectionString = await app.GetConnectionStringAsync("serviceBus");
        connectionString.Should().NotBeNullOrWhiteSpace();

        await using var client = new ServiceBusClient(connectionString);
        var messageId = $"integration-{Guid.NewGuid():N}";
        await using var sender = client.CreateSender("orders");
        await sender.SendMessageAsync(new ServiceBusMessage("emulator round-trip") { MessageId = messageId });

        await using var receiver = client.CreateReceiver("orders", new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
        });
        var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30));

        received.Should().NotBeNull();
        received!.MessageId.Should().Be(messageId);
        received.Body.ToString().Should().Be("emulator round-trip");
    }

    private static async Task<HttpClient> CreateReadyClientAsync(DistributedApplication app)
    {
        var client = app.CreateHttpClient("PicoBusX");
        client.Timeout = TimeSpan.FromSeconds(10);

        for (var attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                var response = await client.GetAsync("/health");
                if (response.IsSuccessStatusCode)
                {
                    return client;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException("The PicoBusX web application did not become healthy within the allotted time.");
    }
}
