using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Options;
using PicoBusX.Web.Options;

namespace PicoBusX.Web.Services;

public class ServiceBusClientFactory : IAsyncDisposable
{
    private readonly ServiceBusConnectionOptions _options;
    private readonly ILogger<ServiceBusClientFactory> _logger;
    private ServiceBusClient? _client;
    private ServiceBusAdministrationClient? _adminClient;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ServiceBusClientFactory(IOptions<ServiceBusConnectionOptions> options,
        ILogger<ServiceBusClientFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ConnectionString);

    public ServiceBusClient GetClient()
    {
        if (_client is not null) return _client;
        if (!IsConfigured)
            throw new InvalidOperationException("Azure Service Bus connection string is not configured.");

        _lock.Wait();
        try
        {
            if (_client is not null) return _client;

            var transportType = _options.TransportType?.ToLowerInvariant() == "amqpwebsockets"
                ? ServiceBusTransportType.AmqpWebSockets
                : ServiceBusTransportType.AmqpTcp;

            _client = new ServiceBusClient(_options.ConnectionString, new ServiceBusClientOptions
            {
                TransportType = transportType
            });
        }
        finally
        {
            _lock.Release();
        }

        return _client;
    }

    public ServiceBusAdministrationClient GetAdminClient()
    {
        if (_adminClient is not null) return _adminClient;
        if (!IsConfigured)
            throw new InvalidOperationException("Azure Service Bus connection string is not configured.");

        _lock.Wait();
        try
        {
            if (_adminClient is not null) return _adminClient;

            var connectionString = _options.AdminConnectionString ?? _options.ConnectionString!;
            var endpoint = ParseConnectionString(connectionString).TryGetValue("Endpoint", out var rawEndpoint)
                ? rawEndpoint
                : "(unknown endpoint)";

            _logger.LogInformation("Creating Service Bus administration client for endpoint {AdminEndpoint}", endpoint);

            var retryOptions = new ServiceBusAdministrationClientOptions
            {
                Retry =
                {
                    MaxRetries = 2,
                    Delay = TimeSpan.FromMilliseconds(200),
                    MaxDelay = TimeSpan.FromSeconds(2),
                    NetworkTimeout = TimeSpan.FromSeconds(10)
                }
            };

            _adminClient = new ServiceBusAdministrationClient(connectionString, retryOptions);
        }
        finally
        {
            _lock.Release();
        }

        return _adminClient;
    }


    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
            _client = null;
        }

        _lock.Dispose();
    }

    private static Dictionary<string, string> ParseConnectionString(string connectionString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var keyValue = part.Split('=', 2);
            if (keyValue.Length == 2)
            {
                result[keyValue[0].Trim()] = keyValue[1].Trim();
            }
        }

        return result;
    }
}