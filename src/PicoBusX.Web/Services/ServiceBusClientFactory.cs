using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Options;
using PicoBusX.Web.Options;

namespace PicoBusX.Web.Services;

public class ServiceBusClientFactory
{
    private readonly ServiceBusConnectionOptions _options;
    private ServiceBusClient? _client;
    private ServiceBusAdministrationClient? _adminClient;

    public ServiceBusClientFactory(IOptions<ServiceBusConnectionOptions> options)
    {
        _options = options.Value;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ConnectionString);

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
        _adminClient = new ServiceBusAdministrationClient(_options.ConnectionString);
        return _adminClient;
    }
}
