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

    public ServiceBusClientFactory(IOptions<ServiceBusConnectionOptions> options, ILogger<ServiceBusClientFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ConnectionString);

    private bool IsLikelyEmulator()
    {
        var connectionString = _options.ConnectionString;
        return connectionString?.Contains("UseDevelopmentEmulator=true", StringComparison.OrdinalIgnoreCase) == true ||
               connectionString?.Contains("localhost", StringComparison.OrdinalIgnoreCase) == true ||
               connectionString?.Contains("127.0.0.1") == true;
    }

    public ServiceBusClient GetClient()
    {
        if (_client is not null) return _client;
        if (!IsConfigured) throw new InvalidOperationException("Azure Service Bus connection string is not configured.");

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
        if (!IsConfigured) throw new InvalidOperationException("Azure Service Bus connection string is not configured.");

        _lock.Wait();
        try
        {
            if (_adminClient is not null) return _adminClient;

            var connectionString = _options.ConnectionString!;

            if (IsLikelyEmulator())
            {
                _logger.LogInformation("Detected emulator connection. Configuring ServiceBusAdministrationClient for emulator.");

                var csDict = ParseConnectionString(connectionString);

                if (csDict.TryGetValue("Endpoint", out var endpoint))
                {
                    var uri = new Uri(endpoint);

                    // Determine admin host/port: use AdminUri if configured (Aspire-mapped port),
                    // otherwise fall back to messaging endpoint host with default admin port 5300.
                    string adminHost;
                    int adminPort;

                    if (!string.IsNullOrWhiteSpace(_options.AdminUri))
                    {
                        var adminUri = new Uri(_options.AdminUri);
                        adminHost = adminUri.Host;
                        adminPort = adminUri.Port;
                        _logger.LogInformation("Using admin URI from configuration: {AdminUri} (host={Host}, port={Port})", _options.AdminUri, adminHost, adminPort);
                    }
                    else
                    {
                        adminHost = uri.Host;
                        adminPort = 5300;
                        _logger.LogWarning("No AdminUri configured, falling back to {Host}:{Port} for admin operations.", adminHost, adminPort);
                    }

                    // ServiceBusAdministrationClient expects sb:// scheme (not http://)
                    // This aligns with EMULATOR_ISSUES.md: Endpoint=sb://localhost:5300
                    var adminEndpoint = $"sb://{adminHost}:{adminPort}";

                    var adminConnectionString = $"Endpoint={adminEndpoint}";
                    if (csDict.TryGetValue("SharedAccessKeyName", out var keyName))
                        adminConnectionString += $";SharedAccessKeyName={keyName}";
                    if (csDict.TryGetValue("SharedAccessKey", out var key))
                        adminConnectionString += $";SharedAccessKey={key}";
                    if (csDict.ContainsKey("UseDevelopmentEmulator"))
                        adminConnectionString += ";UseDevelopmentEmulator=true";

                    _logger.LogInformation("Admin connection string endpoint: {AdminEndpoint}", adminEndpoint);

                    var retryOptions = new ServiceBusAdministrationClientOptions
                    {
                        Retry = {
                            MaxRetries = 2,
                            Delay = TimeSpan.FromMilliseconds(200),
                            MaxDelay = TimeSpan.FromSeconds(2),
                            NetworkTimeout = TimeSpan.FromSeconds(10)
                        }
                    };

                    _adminClient = new ServiceBusAdministrationClient(adminConnectionString, retryOptions);
                    return _adminClient;
                }
            }

            // Standard Azure Service Bus
            _adminClient = new ServiceBusAdministrationClient(connectionString);
        }
        finally
        {
            _lock.Release();
        }
        return _adminClient!;
    }

    public string? GetServiceBusConnectionString()
    {
        return _options.ConnectionString;
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
