namespace PicoBusX.Web.Models;

public class BrowsedMessage
{
    public string MessageId { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? SessionId { get; set; }
    public string? CorrelationId { get; set; }
    public string? ContentType { get; set; }
    public DateTimeOffset EnqueuedTime { get; set; }
    public int DeliveryCount { get; set; }
    public string Body { get; set; } = string.Empty;
    public Dictionary<string, string> ApplicationProperties { get; set; } = new();
    public string? LockToken { get; set; }
    public string? ReceiverEntityPath { get; set; }
}
