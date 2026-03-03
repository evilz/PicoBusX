namespace PicoBusX.Web.Options;

public class ServiceBusConnectionOptions
{
    public const string SectionName = "ServiceBus";

    public string? ConnectionString { get; set; }
    public string TransportType { get; set; } = "AmqpTcp";
    public int EntityMaxPeek { get; set; } = 10;
}
