using Azure.Messaging.ServiceBus.Administration;
using PicoBusX.Web.Models;

namespace PicoBusX.Web.Services;

public class ExplorerService
{
    private readonly ServiceBusClientFactory _factory;
    private readonly ILogger<ExplorerService> _logger;

    public ExplorerService(ServiceBusClientFactory factory, ILogger<ExplorerService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<List<QueueInfo>> GetQueuesAsync(CancellationToken ct = default)
    {
        var admin = _factory.GetAdminClient();
        var result = new List<QueueInfo>();
        await foreach (var queue in admin.GetQueuesAsync(ct))
        {
            QueueRuntimeProperties? runtime = null;
            try { runtime = await admin.GetQueueRuntimePropertiesAsync(queue.Name, ct); } catch { }
            result.Add(new QueueInfo
            {
                Name = queue.Name,
                ActiveMessageCount = runtime?.ActiveMessageCount ?? 0,
                DeadLetterMessageCount = runtime?.DeadLetterMessageCount ?? 0,
                TransferDeadLetterMessageCount = runtime?.TransferDeadLetterMessageCount ?? 0,
                SizeInBytes = runtime?.SizeInBytes ?? 0,
                CreatedAt = runtime?.CreatedAt,
                UpdatedAt = runtime?.UpdatedAt,
                LockDuration = queue.LockDuration,
                MaxDeliveryCount = queue.MaxDeliveryCount,
                RequiresSession = queue.RequiresSession,
                MaxSizeInMegabytes = queue.MaxSizeInMegabytes
            });
        }
        return result;
    }

    public async Task<List<TopicInfo>> GetTopicsAsync(CancellationToken ct = default)
    {
        var admin = _factory.GetAdminClient();
        var result = new List<TopicInfo>();
        await foreach (var topic in admin.GetTopicsAsync(ct))
        {
            TopicRuntimeProperties? runtime = null;
            try { runtime = await admin.GetTopicRuntimePropertiesAsync(topic.Name, ct); } catch { }
            var subs = new List<SubscriptionInfo>();
            await foreach (var sub in admin.GetSubscriptionsAsync(topic.Name, ct))
            {
                SubscriptionRuntimeProperties? subRuntime = null;
                try { subRuntime = await admin.GetSubscriptionRuntimePropertiesAsync(topic.Name, sub.SubscriptionName, ct); } catch { }
                subs.Add(new SubscriptionInfo
                {
                    TopicName = topic.Name,
                    Name = sub.SubscriptionName,
                    ActiveMessageCount = subRuntime?.ActiveMessageCount ?? 0,
                    DeadLetterMessageCount = subRuntime?.DeadLetterMessageCount ?? 0,
                    CreatedAt = subRuntime?.CreatedAt,
                    UpdatedAt = subRuntime?.UpdatedAt,
                    LockDuration = sub.LockDuration,
                    MaxDeliveryCount = sub.MaxDeliveryCount,
                    RequiresSession = sub.RequiresSession
                });
            }
            result.Add(new TopicInfo
            {
                Name = topic.Name,
                SizeInBytes = runtime?.SizeInBytes ?? 0,
                CreatedAt = runtime?.CreatedAt,
                UpdatedAt = runtime?.UpdatedAt,
                MaxSizeInMegabytes = topic.MaxSizeInMegabytes,
                Subscriptions = subs
            });
        }
        return result;
    }
}
