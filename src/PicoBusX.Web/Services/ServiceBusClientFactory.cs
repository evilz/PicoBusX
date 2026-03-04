using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Azure.Core;
using Microsoft.Extensions.Options;
using PicoBusX.Web.Options;

namespace PicoBusX.Web.Services;

public class ServiceBusClientFactory
{
    private readonly ServiceBusConnectionOptions _options;
    private readonly ILogger<ServiceBusClientFactory> _logger;
    private ServiceBusClient? _client;
    private ServiceBusAdministrationClient? _adminClient;

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

        var transportType = _options.TransportType?.ToLowerInvariant() == "amqpwebsockets"
            ? ServiceBusTransportType.AmqpWebSockets
            : ServiceBusTransportType.AmqpTcp;

        _client = new ServiceBusClient(_options.ConnectionString, new ServiceBusClientOptions
        {
            TransportType = transportType
        });
        return _client;
    }

    public ServiceBusAdministrationClient GetAdminClient()
    {
        if (_adminClient is not null) return _adminClient;
        if (!IsConfigured) throw new InvalidOperationException("Azure Service Bus connection string is not configured.");
        
        var connectionString = _options.ConnectionString!;
        
        if (IsLikelyEmulator())
        {
            _logger.LogInformation("Detected emulator connection. Configuring ServiceBusAdministrationClient for emulator.");
            
            var csDict = ParseConnectionString(connectionString);
            
            if (csDict.TryGetValue("Endpoint", out var endpoint))
            {
                var uri = new Uri(endpoint);
                
                // The admin URI from Aspire's emulatorhealth endpoint (http://localhost:{mapped_port})
                // maps to the container's internal port 5300 which handles both health checks and admin operations.
                // We must use the admin URI's host (localhost) and port (dynamic Aspire-mapped port),
                // NOT the messaging endpoint host (which is the container name on Docker network).
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
                    // Fallback: use the messaging endpoint host with default admin port 5300
                    adminHost = uri.Host;
                    adminPort = 5300;
                    _logger.LogWarning("No AdminUri configured, falling back to {Host}:{Port} for admin operations.", adminHost, adminPort);
                }
                
                // Build the admin connection string using the correct host and port for admin operations
                var adminEndpoint = $"http://{adminHost}:{adminPort}";
                
                var adminConnectionString = $"Endpoint={adminEndpoint}";
                if (csDict.TryGetValue("SharedAccessKeyName", out var keyName))
                    adminConnectionString += $";SharedAccessKeyName={keyName}";
                if (csDict.TryGetValue("SharedAccessKey", out var key))
                    adminConnectionString += $";SharedAccessKey={key}";
                if (csDict.ContainsKey("UseDevelopmentEmulator"))
                    adminConnectionString += ";UseDevelopmentEmulator=true";
                
                _logger.LogInformation("Admin connection string endpoint: {AdminEndpoint}", adminEndpoint);
                
                // Use appropriate retry settings
                var options = new ServiceBusAdministrationClientOptions
                {
                    Retry = {
                        MaxRetries = 2,
                        Delay = TimeSpan.FromMilliseconds(200),
                        MaxDelay = TimeSpan.FromSeconds(2),
                        NetworkTimeout = TimeSpan.FromSeconds(10)
                    }
                };
                
                _adminClient = new ServiceBusAdministrationClient(adminConnectionString, options);
                return _adminClient;
            }
        }
        
        // Standard Azure Service Bus
        _adminClient = new ServiceBusAdministrationClient(connectionString);
        return _adminClient;
    }

    public string? GetServiceBusConnectionString()
    {
        return _options.ConnectionString;
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
