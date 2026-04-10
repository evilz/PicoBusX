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

    private async Task ExecuteAdminOperationAsync(
        Func<ServiceBusAdministrationClient, Task> operation,
        Action logSuccess)
    {
        var admin = factory.GetAdminClient();
        await operation(admin);
        logSuccess();
    }
}
