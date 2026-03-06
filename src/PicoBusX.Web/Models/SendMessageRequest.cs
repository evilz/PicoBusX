namespace PicoBusX.Web.Models;

public class SendMessageRequest
{
    public string Destination { get; set; } = string.Empty;
    public string JsonPayload { get; set; } = "{}";
    public string? Subject { get; set; }
    public string? MessageId { get; set; }
    public string? CorrelationId { get; set; }
    public string? SessionId { get; set; }
    public Dictionary<string, string>? ApplicationProperties { get; set; }
    public DateTimeOffset? ScheduledEnqueueTime { get; set; }
}
