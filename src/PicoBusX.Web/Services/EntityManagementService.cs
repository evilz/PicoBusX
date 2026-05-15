using Azure.Messaging.ServiceBus.Administration;

namespace PicoBusX.Web.Services;

public class EntityManagementService(
    ServiceBusClientFactory factory,
    ILogger<EntityManagementService> logger)
{
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

    private async Task ExecuteAdminOperationAsync(
        Func<ServiceBusAdministrationClient, Task> operation,
        Action logSuccess)
    {
        var admin = factory.GetAdminClient();
        await operation(admin);
        logSuccess();
    }
}
