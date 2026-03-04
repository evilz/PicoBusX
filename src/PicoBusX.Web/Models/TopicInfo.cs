namespace PicoBusX.Web.Models;

public class TopicInfo
{
    public string Name { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public long MaxSizeInMegabytes { get; set; }
    public List<SubscriptionInfo> Subscriptions { get; set; } = new();
}
