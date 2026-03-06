using Azure.Messaging.ServiceBus.Administration;

namespace PicoBusX.Web.Services;

public class EntityManagementService(
    ServiceBusClientFactory factory,
    ILogger<EntityManagementService> logger)
{
    public async Task CreateQueueAsync(string name, CancellationToken ct = default)
    {
        var admin = factory.GetAdminClient();
        await admin.CreateQueueAsync(name, ct);
        logger.LogInformation("Created queue {QueueName}", name);
    }

    public async Task CreateTopicAsync(string name, CancellationToken ct = default)
    {
        var admin = factory.GetAdminClient();
        await admin.CreateTopicAsync(name, ct);
        logger.LogInformation("Created topic {TopicName}", name);
    }

    public async Task CreateSubscriptionAsync(string topicName, string subscriptionName, CancellationToken ct = default)
    {
        var admin = factory.GetAdminClient();
        await admin.CreateSubscriptionAsync(topicName, subscriptionName, ct);
        logger.LogInformation("Created subscription {SubscriptionName} on topic {TopicName}", subscriptionName, topicName);
    }

    public async Task DeleteQueueAsync(string name, CancellationToken ct = default)
    {
        var admin = factory.GetAdminClient();
        await admin.DeleteQueueAsync(name, ct);
        logger.LogInformation("Deleted queue {QueueName}", name);
    }

    public async Task DeleteTopicAsync(string name, CancellationToken ct = default)
    {
        var admin = factory.GetAdminClient();
        await admin.DeleteTopicAsync(name, ct);
        logger.LogInformation("Deleted topic {TopicName}", name);
    }

    public async Task DeleteSubscriptionAsync(string topicName, string subscriptionName, CancellationToken ct = default)
    {
        var admin = factory.GetAdminClient();
        await admin.DeleteSubscriptionAsync(topicName, subscriptionName, ct);
        logger.LogInformation("Deleted subscription {SubscriptionName} on topic {TopicName}", subscriptionName, topicName);
    }
}
