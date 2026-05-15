using Azure;
using Azure.Messaging.ServiceBus.Administration;
using PicoBusX.Web.Models;

namespace PicoBusX.Web.Services;

public class ExplorerService(
    ServiceBusClientFactory factory,
    ILogger<ExplorerService> logger) : IExplorerService
{
    public async Task<ExplorerLoadResult> LoadAsync(CancellationToken ct = default)
    {
        try
        {
            var queues = await GetQueuesAsync(ct);
            var topics = await GetTopicsAsync(ct);

            return new ExplorerLoadResult
            {
                Queues = queues,
                Topics = topics
            };
        }
        catch (Exception ex) when (IsConnectivityError(ex))
        {
            logger.LogWarning(ex, "Service Bus administration endpoint is unreachable.");

            return new ExplorerLoadResult
            {
                WarningMessage = "Unable to reach the Service Bus administration endpoint right now. Check network access or the emulator, then refresh to try again."
            };
        }
        catch (Exception ex) when (TryBuildUserFacingError(ex, out var message))
        {
            logger.LogError(ex, "Service Bus metadata loading failed with a user-facing error.");

            return new ExplorerLoadResult
            {
                ErrorMessage = message
            };
        }
    }

    public async Task<List<QueueInfo>> GetQueuesAsync(CancellationToken ct = default)
    {
        var result = new List<QueueInfo>();
        var admin = factory.GetAdminClient();

        await foreach (var queue in admin.GetQueuesAsync(ct))
        {
            var runtime = await TryGetQueueRuntimePropertiesAsync(admin, queue.Name, ct);

            result.Add(new QueueInfo
            {
                Name = queue.Name,
                ActiveMessageCount = runtime?.ActiveMessageCount ?? 0,
                DeadLetterMessageCount = runtime?.DeadLetterMessageCount ?? 0,
                TransferDeadLetterMessageCount = runtime?.TransferDeadLetterMessageCount ?? 0,
                ScheduledMessageCount = runtime?.ScheduledMessageCount ?? 0,
                TransferMessageCount = runtime?.TransferMessageCount ?? 0,
                SizeInBytes = runtime?.SizeInBytes ?? 0,
                CreatedAt = NormalizeTimestamp(runtime?.CreatedAt),
                UpdatedAt = NormalizeTimestamp(runtime?.UpdatedAt),
                AccessedAt = NormalizeTimestamp(runtime?.AccessedAt),
                LockDuration = queue.LockDuration,
                MaxDeliveryCount = queue.MaxDeliveryCount,
                RequiresSession = queue.RequiresSession,
                MaxSizeInMegabytes = queue.MaxSizeInMegabytes,
                DefaultMessageTimeToLive = queue.DefaultMessageTimeToLive,
                AutoDeleteOnIdle = queue.AutoDeleteOnIdle,
                EnablePartitioning = queue.EnablePartitioning,
                EnableBatchedOperations = queue.EnableBatchedOperations,
                ForwardTo = NormalizeText(queue.ForwardTo),
                ForwardDeadLetteredMessagesTo = NormalizeText(queue.ForwardDeadLetteredMessagesTo),
                DeadLetteringOnMessageExpiration = queue.DeadLetteringOnMessageExpiration,
                Status = queue.Status.ToString()
            });
        }

        return result;
    }

    public async Task<List<TopicInfo>> GetTopicsAsync(CancellationToken ct = default)
    {
        var result = new List<TopicInfo>();
        var admin = factory.GetAdminClient();

        await foreach (var topic in admin.GetTopicsAsync(ct))
        {
            var runtime = await TryGetTopicRuntimePropertiesAsync(admin, topic.Name, ct);
            var subscriptions = await GetSubscriptionsForTopicAsync(admin, topic.Name, ct);

            result.Add(new TopicInfo
            {
                Name = topic.Name,
                SizeInBytes = runtime?.SizeInBytes ?? 0,
                ScheduledMessageCount = runtime?.ScheduledMessageCount ?? 0,
                CreatedAt = NormalizeTimestamp(runtime?.CreatedAt),
                UpdatedAt = NormalizeTimestamp(runtime?.UpdatedAt),
                AccessedAt = NormalizeTimestamp(runtime?.AccessedAt),
                MaxSizeInMegabytes = topic.MaxSizeInMegabytes,
                DefaultMessageTimeToLive = topic.DefaultMessageTimeToLive,
                AutoDeleteOnIdle = topic.AutoDeleteOnIdle,
                EnablePartitioning = topic.EnablePartitioning,
                EnableBatchedOperations = topic.EnableBatchedOperations,
                SupportOrdering = topic.SupportOrdering,
                DuplicateDetectionHistoryTimeWindow = topic.DuplicateDetectionHistoryTimeWindow,
                RequiresDuplicateDetection = topic.RequiresDuplicateDetection,
                Status = topic.Status.ToString(),
                Subscriptions = subscriptions
            });
        }

        return result;
    }

    private static async Task<T?> TryGetRuntimePropertiesAsync<T>(
        Func<Task<T>> fetch,
        Action<Exception> onFailure) where T : class
    {
        try
        {
            return await fetch();
        }
        catch (Exception ex) when (!TryBuildUserFacingError(ex, out _))
        {
            onFailure(ex);
            return null;
        }
    }

    private Task<QueueRuntimeProperties?> TryGetQueueRuntimePropertiesAsync(
        ServiceBusAdministrationClient admin,
        string queueName,
        CancellationToken ct) =>
        TryGetRuntimePropertiesAsync<QueueRuntimeProperties>(
            async () => await admin.GetQueueRuntimePropertiesAsync(queueName, ct),
            ex => logger.LogWarning(ex, "Failed to get runtime properties for queue {QueueName}", queueName));

    private Task<TopicRuntimeProperties?> TryGetTopicRuntimePropertiesAsync(
        ServiceBusAdministrationClient admin,
        string topicName,
        CancellationToken ct) =>
        TryGetRuntimePropertiesAsync<TopicRuntimeProperties>(
            async () => await admin.GetTopicRuntimePropertiesAsync(topicName, ct),
            ex => logger.LogWarning(ex, "Failed to get runtime properties for topic {TopicName}", topicName));

    private async Task<List<SubscriptionInfo>> GetSubscriptionsForTopicAsync(
        ServiceBusAdministrationClient admin,
        string topicName,
        CancellationToken ct)
    {
        var subscriptions = new List<SubscriptionInfo>();

        try
        {
            await foreach (var subscription in admin.GetSubscriptionsAsync(topicName, ct))
            {
                var runtime = await TryGetSubscriptionRuntimePropertiesAsync(admin, topicName, subscription.SubscriptionName, ct);

                var rules = await GetRulesForSubscriptionAsync(admin, topicName, subscription.SubscriptionName, ct);

                subscriptions.Add(new SubscriptionInfo
                {
                    TopicName = topicName,
                    Name = subscription.SubscriptionName,
                    ActiveMessageCount = runtime?.ActiveMessageCount ?? 0,
                    DeadLetterMessageCount = runtime?.DeadLetterMessageCount ?? 0,
                    TransferMessageCount = runtime?.TransferMessageCount ?? 0,
                    TransferDeadLetterMessageCount = runtime?.TransferDeadLetterMessageCount ?? 0,
                    CreatedAt = NormalizeTimestamp(runtime?.CreatedAt),
                    UpdatedAt = NormalizeTimestamp(runtime?.UpdatedAt),
                    AccessedAt = NormalizeTimestamp(runtime?.AccessedAt),
                    LockDuration = subscription.LockDuration,
                    MaxDeliveryCount = subscription.MaxDeliveryCount,
                    RequiresSession = subscription.RequiresSession,
                    DefaultMessageTimeToLive = subscription.DefaultMessageTimeToLive,
                    AutoDeleteOnIdle = subscription.AutoDeleteOnIdle,
                    EnableBatchedOperations = subscription.EnableBatchedOperations,
                    DeadLetteringOnMessageExpiration = subscription.DeadLetteringOnMessageExpiration,
                    DeadLetteringOnFilterEvaluationExceptions = subscription.EnableDeadLetteringOnFilterEvaluationExceptions,
                    ForwardTo = NormalizeText(subscription.ForwardTo),
                    ForwardDeadLetteredMessagesTo = NormalizeText(subscription.ForwardDeadLetteredMessagesTo),
                    Status = subscription.Status.ToString(),
                    Rules = rules
                });
            }
        }
        catch (Exception ex) when (!TryBuildUserFacingError(ex, out _))
        {
            logger.LogWarning(ex, "Failed to get subscriptions for topic {TopicName}", topicName);
        }

        return subscriptions;
    }

    private async Task<List<RuleInfo>> GetRulesForSubscriptionAsync(
        ServiceBusAdministrationClient admin,
        string topicName,
        string subscriptionName,
        CancellationToken ct)
    {
        var rules = new List<RuleInfo>();
        try
        {
            await foreach (var rule in admin.GetRulesAsync(topicName, subscriptionName, ct))
            {
                rules.Add(new RuleInfo
                {
                    Name = rule.Name,
                    FilterType = GetFilterType(rule.Filter),
                    FilterExpression = GetFilterExpression(rule.Filter),
                    ActionExpression = rule.Action is SqlRuleAction sqlAction ? sqlAction.SqlExpression : null
                });
            }
        }
        catch (Exception ex) when (!TryBuildUserFacingError(ex, out _))
        {
            logger.LogWarning(
                ex,
                "Failed to get rules for subscription {SubscriptionName} on topic {TopicName}",
                subscriptionName,
                topicName);
        }

        return rules;
    }

    private static string GetFilterType(RuleFilter? filter) => filter switch
    {
        TrueRuleFilter => "True",
        FalseRuleFilter => "False",
        SqlRuleFilter => "SQL",
        CorrelationRuleFilter => "Correlation",
        _ => "Unknown"
    };

    private static string? GetFilterExpression(RuleFilter? filter) => filter switch
    {
        TrueRuleFilter => "1=1",
        FalseRuleFilter => "1=0",
        SqlRuleFilter sql => sql.SqlExpression,
        CorrelationRuleFilter correlation => FormatCorrelationFilter(correlation),
        _ => null
    };

    private static string FormatCorrelationFilter(CorrelationRuleFilter filter)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(filter.CorrelationId)) parts.Add($"CorrelationId={filter.CorrelationId}");
        if (!string.IsNullOrEmpty(filter.MessageId)) parts.Add($"MessageId={filter.MessageId}");
        if (!string.IsNullOrEmpty(filter.To)) parts.Add($"To={filter.To}");
        if (!string.IsNullOrEmpty(filter.ReplyTo)) parts.Add($"ReplyTo={filter.ReplyTo}");
        if (!string.IsNullOrEmpty(filter.Subject)) parts.Add($"Subject={filter.Subject}");
        if (!string.IsNullOrEmpty(filter.SessionId)) parts.Add($"SessionId={filter.SessionId}");
        if (!string.IsNullOrEmpty(filter.ReplyToSessionId)) parts.Add($"ReplyToSessionId={filter.ReplyToSessionId}");
        if (!string.IsNullOrEmpty(filter.ContentType)) parts.Add($"ContentType={filter.ContentType}");
        return parts.Count > 0 ? string.Join(", ", parts) : "—";
    }

    private Task<SubscriptionRuntimeProperties?> TryGetSubscriptionRuntimePropertiesAsync(
        ServiceBusAdministrationClient admin,
        string topicName,
        string subscriptionName,
        CancellationToken ct) =>
        TryGetRuntimePropertiesAsync<SubscriptionRuntimeProperties>(
            async () => await admin.GetSubscriptionRuntimePropertiesAsync(topicName, subscriptionName, ct),
            ex => logger.LogWarning(
                ex,
                "Failed to get runtime properties for subscription {SubscriptionName} on topic {TopicName}",
                subscriptionName,
                topicName));

    private static DateTimeOffset? NormalizeTimestamp(DateTimeOffset? value)
    {
        return value is null || value == DateTimeOffset.MinValue ? null : value;
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool IsConnectivityError(Exception ex)
    {
        if (ex is TaskCanceledException)
        {
            return true;
        }

        if (ex.InnerException is System.Net.Sockets.SocketException or IOException)
        {
            return true;
        }

        if (ex is RequestFailedException requestFailedException)
        {
            return requestFailedException.Status is 0 or 408 or 503 or 504;
        }

        return false;
    }

    private static bool TryBuildUserFacingError(Exception ex, out string message)
    {
        if (ex is InvalidOperationException ioe &&
            (ioe.Message.Contains("connection string", StringComparison.OrdinalIgnoreCase) ||
             ioe.Message.Contains("not configured", StringComparison.OrdinalIgnoreCase)))
        {
            message = "Service Bus is not configured. Configure the connection in Settings.";
            return true;
        }

        if (ex is Azure.Identity.AuthenticationFailedException)
        {
            message = "Azure authentication failed. Verify your managed identity or service principal credentials in Settings.";
            return true;
        }

        if (ex is RequestFailedException requestFailedException)
        {
            switch (requestFailedException.Status)
            {
                case 400:
                    message = "Service Bus configuration is invalid. Verify the endpoint and connection string values.";
                    return true;
                case 401:
                    message = "Authentication failed when loading Service Bus metadata. Verify the connection string or SAS policy.";
                    return true;
                case 403:
                    message = "Access to Service Bus metadata was denied. Verify the SAS policy includes the required rights.";
                    return true;
            }
        }

        if (ex is not InvalidOperationException)
        {
            message = "The explorer could not load Service Bus metadata. Try refreshing or review the application logs for details.";
            return true;
        }

        message = string.Empty;
        return false;
    }

}