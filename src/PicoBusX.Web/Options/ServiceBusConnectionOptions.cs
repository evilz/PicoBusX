namespace PicoBusX.Web.Options;

public class ServiceBusConnectionOptions
{
    public const string SectionName = "ServiceBus";

    [ConfigurationKeyName("SERVICEBUS_CONNECTIONSTRING")]
    public string? ConnectionString { get; set; }
    
    [ConfigurationKeyName("SERVICEBUS_ADMINCONNECTIONSTRING")]
    public string? AdminConnectionString { get; set; }
    public string TransportType { get; set; } = "AmqpTcp";
    public int EntityMaxPeek { get; set; } = 10;
    
}
