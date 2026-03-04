namespace PicoBusX.Web.Options;

public class ServiceBusConnectionOptions
{
    public const string SectionName = "ServiceBus";

    public string? ConnectionString { get; set; }
    public string? AdminUri { get; set; }
    public string TransportType { get; set; } = "AmqpTcp";
    public int EntityMaxPeek { get; set; } = 10;
    
    /// <summary>
    /// Comma-separated list of known queue names (from AppHost pre-created entities).
    /// Used as fallback when emulator doesn't support listing operations.
    /// </summary>
    public string? KnownQueues { get; set; }
    
    /// <summary>
    /// Comma-separated list of known topic names.
    /// </summary>
    public string? KnownTopics { get; set; }
    
    /// <summary>
    /// Comma-separated list of known subscriptions in format "TopicName:SubscriptionName".
    /// </summary>
    public string? KnownSubscriptions { get; set; }
}
