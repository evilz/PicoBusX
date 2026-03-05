using Azure.Messaging.ServiceBus.Administration;
using PicoBusX.Web.Models;

namespace PicoBusX.Web.Services;

public class ExplorerService(
    ServiceBusClientFactory factory,
    ILogger<ExplorerService> logger)
{
    public async Task<List<QueueInfo>> GetQueuesAsync(CancellationToken ct = default)
    {
        var result = new List<QueueInfo>();

        try
        {
            var admin = factory.GetAdminClient();

            // Try listing queues first (works with real Azure Service Bus)
            try
            {
                await foreach (var queue in admin.GetQueuesAsync(ct))
                {
                    QueueRuntimeProperties? runtime = null;
                    try
                    {
                        runtime = await admin.GetQueueRuntimePropertiesAsync(queue.Name, ct);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to get runtime properties for queue {QueueName}", queue.Name);
                    }

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
            catch (Exception ex)
            {
                logger.LogError(ex, "Listing queues failed.");
            }
        }
        catch (Exception ex) when (IsConnectivityError(ex))
        {
            logger.LogWarning(ex,
                "Admin client not available. This may be due to emulator port 5300 not being exposed. Returning empty queue list.");
            logger.LogInformation(
                "Tip: Ensure the Azure Service Bus emulator is running and port 5300 is exposed for management operations.");
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve queues from Service Bus.");
            throw;
        }

        return result;
    }

    public async Task<List<TopicInfo>> GetTopicsAsync(CancellationToken ct = default)
    {
        var result = new List<TopicInfo>();

        try
        {
            var admin = factory.GetAdminClient();


            await foreach (var topic in admin.GetTopicsAsync(ct))
            {
                TopicRuntimeProperties? runtime = null;
                try
                {
                    runtime = await admin.GetTopicRuntimePropertiesAsync(topic.Name, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to get runtime properties for topic {TopicName}", topic.Name);
                }

                var subs = await GetSubscriptionsForTopicAsync(admin, topic.Name, ct);

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
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to retrieve topics from Service Bus. Connection string or endpoint may be incorrect.");
            throw;
        }
    }

    /// <summary>
    /// Returns true only for transient connectivity failures (socket/network errors, HTTP 503/504/408).
    /// Auth and configuration errors (401, 403, 400) are NOT matched — those should propagate so
    /// the UI can surface a meaningful "invalid credentials" or "bad endpoint" error.
    /// </summary>
    private static bool IsConnectivityError(Exception ex)
    {
        if (ex.InnerException is System.Net.Sockets.SocketException or IOException)
            return true;

        if (ex is Azure.RequestFailedException rfe)
            return rfe.Status is 503 or 504 or 408 or 0;

        return false;
    }

    private async Task<List<SubscriptionInfo>> GetSubscriptionsForTopicAsync(
        ServiceBusAdministrationClient admin, string topicName, CancellationToken ct)
    {
        var subs = new List<SubscriptionInfo>();
        try
        {
            await foreach (var sub in admin.GetSubscriptionsAsync(topicName, ct))
            {
                SubscriptionRuntimeProperties? subRuntime = null;
                try
                {
                    subRuntime = await admin.GetSubscriptionRuntimePropertiesAsync(topicName, sub.SubscriptionName, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Failed to get runtime properties for subscription {SubscriptionName} on topic {TopicName}",
                        sub.SubscriptionName, topicName);
                }

                subs.Add(new SubscriptionInfo
                {
                    TopicName = topicName,
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
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get subscriptions for topic {TopicName}", topicName);
        }

        return subs;
    }

}