using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Options;
using PicoBusX.Web.Options;

namespace PicoBusX.Web.Services;

public class ServiceBusClientFactory : IAsyncDisposable
{
    private readonly ServiceBusConnectionOptions _options;
    private readonly IConnectionSettingsStore _settingsStore;
    private readonly ILogger<ServiceBusClientFactory> _logger;
    private ServiceBusClient? _client;
    private ServiceBusAdministrationClient? _adminClient;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ServiceBusClientFactory(
        IOptions<ServiceBusConnectionOptions> options,
        IConnectionSettingsStore settingsStore,
        ILogger<ServiceBusClientFactory> logger)
    {
        _options = options.Value;
        _settingsStore = settingsStore;
        _logger = logger;
    }

    /// <summary>Returns the effective options: runtime store takes priority over appsettings / env.</summary>
    private ServiceBusConnectionOptions GetEffectiveOptions() =>
        _settingsStore.GetRuntimeSettings() ?? _options;

    public bool IsConfigured
    {
        get
        {
            var opts = GetEffectiveOptions();
            return opts.AuthType switch
            {
                ServiceBusConnectionOptions.AuthTypeDefaultAzureCredential =>
                    !string.IsNullOrWhiteSpace(opts.FullyQualifiedNamespace),

                ServiceBusConnectionOptions.AuthTypeServicePrincipal =>
                    !string.IsNullOrWhiteSpace(opts.FullyQualifiedNamespace)
                    && !string.IsNullOrWhiteSpace(opts.TenantId)
                    && !string.IsNullOrWhiteSpace(opts.ClientId)
                    && !string.IsNullOrWhiteSpace(opts.ClientSecret),

                _ => !string.IsNullOrWhiteSpace(opts.ConnectionString),
            };
        }
    }

    /// <summary>
    /// Disposes any cached clients so that the next call to <see cref="GetClient"/> or
    /// <see cref="GetAdminClient"/> creates fresh clients from the current settings.
    /// Call this after updating the connection settings at runtime.
    /// </summary>
    public async Task ResetClientsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_client is not null)
            {
                await _client.DisposeAsync();
                _client = null;
            }

            // ServiceBusAdministrationClient does not implement IDisposable – it is a stateless
            // HTTP client wrapper with no long-lived connections to release.
            _adminClient = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Tests connectivity using the provided <paramref name="settings"/> without updating the
    /// stored settings or cached clients. Throws on failure.
    /// </summary>
    public static async Task TestConnectionAsync(ServiceBusConnectionOptions settings, CancellationToken ct = default)
    {
        var admin = CreateAdminClientFromOptions(settings);
        await foreach (var _ in admin.GetQueuesAsync(ct))
        {
            break; // A single successful page confirms connectivity.
        }
    }

    public ServiceBusClient GetClient() =>
        GetOrCreate(ref _client, () => CreateClient(GetEffectiveOptions()));

    public ServiceBusAdministrationClient GetAdminClient() =>
        GetOrCreate(ref _adminClient, () => CreateAdminClientFromOptions(GetEffectiveOptions(), _logger));

    private T GetOrCreate<T>(ref T? field, Func<T> factory) where T : class
    {
        if (field is not null) return field;
        if (!IsConfigured)
            throw new InvalidOperationException("Azure Service Bus is not configured. Please configure the connection in Settings.");

        _lock.Wait();
        try
        {
            if (field is null)
                field = factory();
        }
        finally
        {
            _lock.Release();
        }

        return field;
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

    private ServiceBusClient CreateClient(ServiceBusConnectionOptions opts)
    {
        var transportType = opts.TransportType?.ToLowerInvariant() == "amqpwebsockets"
            ? ServiceBusTransportType.AmqpWebSockets
            : ServiceBusTransportType.AmqpTcp;

        var clientOptions = new ServiceBusClientOptions { TransportType = transportType };

        return opts.AuthType switch
        {
            ServiceBusConnectionOptions.AuthTypeDefaultAzureCredential => new ServiceBusClient(
                NormalizeNamespace(opts.FullyQualifiedNamespace!),
                new DefaultAzureCredential(),
                clientOptions),

            ServiceBusConnectionOptions.AuthTypeServicePrincipal => new ServiceBusClient(
                NormalizeNamespace(opts.FullyQualifiedNamespace!),
                new ClientSecretCredential(opts.TenantId!, opts.ClientId!, opts.ClientSecret!),
                clientOptions),

            _ => new ServiceBusClient(opts.ConnectionString!, clientOptions),
        };
    }

    private ServiceBusAdministrationClient CreateAdminClientFromOptions(ServiceBusConnectionOptions opts,
        bool logNamespace = false)
    {
        return CreateAdminClientFromOptions(opts, logNamespace ? _logger : null);
    }

    private static ServiceBusAdministrationClient CreateAdminClientFromOptions(ServiceBusConnectionOptions opts,
        ILogger? logger = null)
    {
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

        if (opts.AuthType == ServiceBusConnectionOptions.AuthTypeConnectionString)
        {
            var connectionString = opts.AdminConnectionString ?? opts.ConnectionString!;
            if (logger is not null)
            {
                var endpoint = ParseConnectionString(connectionString).TryGetValue("Endpoint", out var rawEndpoint)
                    ? rawEndpoint
                    : "(unknown endpoint)";
                logger.LogInformation("Creating Service Bus administration client for endpoint {AdminEndpoint}", endpoint);
            }

            return new ServiceBusAdministrationClient(connectionString, retryOptions);
        }

        var ns = NormalizeNamespace(opts.FullyQualifiedNamespace!);
        logger?.LogInformation("Creating Service Bus administration client for namespace {Namespace}", ns);

        Azure.Core.TokenCredential credential = opts.AuthType == ServiceBusConnectionOptions.AuthTypeServicePrincipal
            ? new ClientSecretCredential(opts.TenantId!, opts.ClientId!, opts.ClientSecret!)
            : new DefaultAzureCredential();

        return new ServiceBusAdministrationClient(ns, credential, retryOptions);
    }

    /// <summary>Strips any URI scheme prefix and normalises to a bare FQNS hostname.</summary>
    private static string NormalizeNamespace(string ns)
    {
        if (ns.StartsWith("sb://", StringComparison.OrdinalIgnoreCase))
            ns = ns[5..];
        else if (ns.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            ns = ns[8..];

        ns = ns.TrimEnd('/');

        // Append the standard suffix when only a short namespace name is provided
        // (i.e., no dots – so "myns" becomes "myns.servicebus.windows.net").
        if (!ns.EndsWith(".servicebus.windows.net", StringComparison.OrdinalIgnoreCase) && !ns.Contains('.'))
            ns = $"{ns}.servicebus.windows.net";

        return ns;
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