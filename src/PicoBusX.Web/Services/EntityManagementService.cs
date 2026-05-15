using Azure.Messaging.ServiceBus.Administration;

namespace PicoBusX.Web.Services;

public class EntityManagementService(
    ServiceBusClientFactory factory,
    ILogger<EntityManagementService> logger)
{
    public sealed record QueueUpdateOptions(
        TimeSpan LockDuration,
        int MaxDeliveryCount,
        TimeSpan DefaultMessageTimeToLive,
        TimeSpan AutoDeleteOnIdle,
        bool EnableBatchedOperations,
        bool DeadLetteringOnMessageExpiration,
        string? ForwardTo,
        string? ForwardDeadLetteredMessagesTo);

    public sealed record TopicUpdateOptions(
        TimeSpan DefaultMessageTimeToLive,
        TimeSpan AutoDeleteOnIdle,
        bool EnableBatchedOperations,
        bool SupportOrdering);

    public sealed record SubscriptionUpdateOptions(
        TimeSpan LockDuration,
        int MaxDeliveryCount,
        TimeSpan DefaultMessageTimeToLive,
        TimeSpan AutoDeleteOnIdle,
        bool EnableBatchedOperations,
        bool DeadLetteringOnMessageExpiration,
        bool DeadLetteringOnFilterEvaluationExceptions,
        string? ForwardTo,
        string? ForwardDeadLetteredMessagesTo);

    public Task CreateQueueAsync(string name, CancellationToken ct = default) =>
        ExecuteAdminOperationAsync(
            admin => admin.CreateQueueAsync(name, ct),
            () => logger.LogInformation("Created queue {QueueName}", name));

    public Task CreateTopicAsync(string name, CancellationToken ct = default) =>
        ExecuteAdminOperationAsync(
            admin => admin.CreateTopicAsync(name, ct),
            () => logger.LogInformation("Created topic {TopicName}", name));

    public Task CreateSubscriptionAsync(string topicName, string subscriptionName, CancellationToken ct = default) =>
        ExecuteAdminOperationAsync(
            admin => admin.CreateSubscriptionAsync(topicName, subscriptionName, ct),
            () => logger.LogInformation("Created subscription {SubscriptionName} on topic {TopicName}", subscriptionName, topicName));

    public Task DeleteQueueAsync(string name, CancellationToken ct = default) =>
        ExecuteAdminOperationAsync(
            admin => admin.DeleteQueueAsync(name, ct),
            () => logger.LogInformation("Deleted queue {QueueName}", name));

    public Task DeleteTopicAsync(string name, CancellationToken ct = default) =>
        ExecuteAdminOperationAsync(
            admin => admin.DeleteTopicAsync(name, ct),
            () => logger.LogInformation("Deleted topic {TopicName}", name));

    public Task DeleteSubscriptionAsync(string topicName, string subscriptionName, CancellationToken ct = default) =>
        ExecuteAdminOperationAsync(
            admin => admin.DeleteSubscriptionAsync(topicName, subscriptionName, ct),
            () => logger.LogInformation("Deleted subscription {SubscriptionName} on topic {TopicName}", subscriptionName, topicName));

    public Task UpdateQueueAsync(string name, QueueUpdateOptions options, CancellationToken ct = default) =>
        ExecuteAdminOperationAsync(
            async admin =>
            {
                var queue = (await admin.GetQueueAsync(name, ct)).Value;
                queue.LockDuration = options.LockDuration;
                queue.MaxDeliveryCount = options.MaxDeliveryCount;
                queue.DefaultMessageTimeToLive = options.DefaultMessageTimeToLive;
                queue.AutoDeleteOnIdle = options.AutoDeleteOnIdle;
                queue.EnableBatchedOperations = options.EnableBatchedOperations;
                queue.DeadLetteringOnMessageExpiration = options.DeadLetteringOnMessageExpiration;
                queue.ForwardTo = NormalizeText(options.ForwardTo);
                queue.ForwardDeadLetteredMessagesTo = NormalizeText(options.ForwardDeadLetteredMessagesTo);
                await admin.UpdateQueueAsync(queue, ct);
            },
            () => logger.LogInformation("Updated queue {QueueName}", name));

    public Task UpdateTopicAsync(string name, TopicUpdateOptions options, CancellationToken ct = default) =>
        ExecuteAdminOperationAsync(
            async admin =>
            {
                var topic = (await admin.GetTopicAsync(name, ct)).Value;
                topic.DefaultMessageTimeToLive = options.DefaultMessageTimeToLive;
                topic.AutoDeleteOnIdle = options.AutoDeleteOnIdle;
                topic.EnableBatchedOperations = options.EnableBatchedOperations;
                topic.SupportOrdering = options.SupportOrdering;
                await admin.UpdateTopicAsync(topic, ct);
            },
            () => logger.LogInformation("Updated topic {TopicName}", name));

    public Task UpdateSubscriptionAsync(string topicName, string subscriptionName, SubscriptionUpdateOptions options, CancellationToken ct = default) =>
        ExecuteAdminOperationAsync(
            async admin =>
            {
                var subscription = (await admin.GetSubscriptionAsync(topicName, subscriptionName, ct)).Value;
                subscription.LockDuration = options.LockDuration;
                subscription.MaxDeliveryCount = options.MaxDeliveryCount;
                subscription.DefaultMessageTimeToLive = options.DefaultMessageTimeToLive;
                subscription.AutoDeleteOnIdle = options.AutoDeleteOnIdle;
                subscription.EnableBatchedOperations = options.EnableBatchedOperations;
                subscription.DeadLetteringOnMessageExpiration = options.DeadLetteringOnMessageExpiration;
                subscription.EnableDeadLetteringOnFilterEvaluationExceptions = options.DeadLetteringOnFilterEvaluationExceptions;
                subscription.ForwardTo = NormalizeText(options.ForwardTo);
                subscription.ForwardDeadLetteredMessagesTo = NormalizeText(options.ForwardDeadLetteredMessagesTo);
                await admin.UpdateSubscriptionAsync(subscription, ct);
            },
            () => logger.LogInformation("Updated subscription {SubscriptionName} on topic {TopicName}", subscriptionName, topicName));

    private async Task ExecuteAdminOperationAsync(
        Func<ServiceBusAdministrationClient, Task> operation,
        Action logSuccess)
    {
        var admin = factory.GetAdminClient();
        await operation(admin);
        logSuccess();
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
