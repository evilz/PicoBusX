using Azure;
using Azure.Messaging.ServiceBus;
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

    public Task CreateQueueAsync(CreateQueueOptions options, CancellationToken ct = default) =>
        ExecuteAdminOperationAsync(
            admin => admin.CreateQueueAsync(options, ct),
            () => logger.LogInformation("Created queue {QueueName}", options.Name));

    public Task CreateQueueAsync(string name, CancellationToken ct = default) =>
        CreateQueueAsync(new CreateQueueOptions(name), ct);

    public Task CreateTopicAsync(CreateTopicOptions options, CancellationToken ct = default) =>
        ExecuteAdminOperationAsync(
            admin => admin.CreateTopicAsync(options, ct),
            () => logger.LogInformation("Created topic {TopicName}", options.Name));

    public Task CreateTopicAsync(string name, CancellationToken ct = default) =>
        CreateTopicAsync(new CreateTopicOptions(name), ct);

    public Task CreateSubscriptionAsync(CreateSubscriptionOptions options, CancellationToken ct = default) =>
        ExecuteAdminOperationAsync(
            admin => admin.CreateSubscriptionAsync(options, ct),
            () => logger.LogInformation("Created subscription {SubscriptionName} on topic {TopicName}", options.SubscriptionName, options.TopicName));

    public Task CreateSubscriptionAsync(string topicName, string subscriptionName, CancellationToken ct = default) =>
        CreateSubscriptionAsync(new CreateSubscriptionOptions(topicName, subscriptionName), ct);

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

    public Task UpsertSubscriptionSqlRuleAsync(
        string topicName,
        string subscriptionName,
        string ruleName,
        string sqlFilterExpression,
        CancellationToken ct = default) =>
        ExecuteAdminOperationAsync(
            async admin =>
            {
                try
                {
                    var response = await admin.GetRuleAsync(topicName, subscriptionName, ruleName, ct);
                    var rule = response.Value;
                    // Update the filter in-place to preserve any existing SQL action on the rule.
                    rule.Filter = new SqlRuleFilter(sqlFilterExpression);
                    await admin.UpdateRuleAsync(topicName, subscriptionName, rule, ct);
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    var options = new CreateRuleOptions(ruleName, new SqlRuleFilter(sqlFilterExpression));
                    await admin.CreateRuleAsync(topicName, subscriptionName, options, ct);
                }
                catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
                {
                    var options = new CreateRuleOptions(ruleName, new SqlRuleFilter(sqlFilterExpression));
                    await admin.CreateRuleAsync(topicName, subscriptionName, options, ct);
                }
            },
            () => logger.LogInformation(
                "Saved SQL rule {RuleName} on subscription {SubscriptionName} and topic {TopicName}",
                ruleName,
                subscriptionName,
                topicName));

    public Task DeleteSubscriptionRuleAsync(
        string topicName,
        string subscriptionName,
        string ruleName,
        CancellationToken ct = default) =>
        ExecuteAdminOperationAsync(
            admin => admin.DeleteRuleAsync(topicName, subscriptionName, ruleName, ct),
            () => logger.LogInformation(
                "Deleted rule {RuleName} on subscription {SubscriptionName} and topic {TopicName}",
                ruleName,
                subscriptionName,
                topicName));

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
