using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace PicoBusX.Web.Services;

public class EntityManagementService(
    ServiceBusClientFactory factory,
    ILogger<EntityManagementService> logger)
{
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
}
