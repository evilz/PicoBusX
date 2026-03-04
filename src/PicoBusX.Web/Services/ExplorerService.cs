using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Options;
using PicoBusX.Web.Models;
using PicoBusX.Web.Options;

namespace PicoBusX.Web.Services;

public class ExplorerService
{
    private readonly ServiceBusClientFactory _factory;
    private readonly ServiceBusConnectionOptions _options;
    private readonly ILogger<ExplorerService> _logger;

    public ExplorerService(ServiceBusClientFactory factory, IOptions<ServiceBusConnectionOptions> options, ILogger<ExplorerService> logger)
    {
        _factory = factory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<List<QueueInfo>> GetQueuesAsync(CancellationToken ct = default)
    {
        var result = new List<QueueInfo>();
        
        try
        {
            var admin = _factory.GetAdminClient();
            
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
                        _logger.LogWarning(ex, "Failed to get runtime properties for queue {QueueName}", queue.Name);
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
            catch (Exception ex) when (IsEmulatorListingNotSupported(ex))
            {
                _logger.LogWarning(ex, "Listing queues not supported by emulator. Falling back to known entities.");
                result = await GetQueuesFromKnownEntitiesAsync(admin, ct);
            }
        }
        catch (Exception ex) when (IsConnectivityError(ex))
        {
            _logger.LogWarning(ex, "Admin client not available. This may be due to emulator port 5300 not being exposed. Returning empty queue list.");
            _logger.LogInformation("Tip: Ensure the Azure Service Bus emulator is running and port 5300 is exposed for management operations.");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve queues from Service Bus.");
            throw;
        }
        
        return result;
    }

    public async Task<List<TopicInfo>> GetTopicsAsync(CancellationToken ct = default)
    {
        var result = new List<TopicInfo>();
        
        try
        {
            var admin = _factory.GetAdminClient();
            
            // Try listing topics first (works with real Azure Service Bus)
            try
            {
                await foreach (var topic in admin.GetTopicsAsync(ct))
                {
                    TopicRuntimeProperties? runtime = null;
                    try 
                    { 
                        runtime = await admin.GetTopicRuntimePropertiesAsync(topic.Name, ct); 
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get runtime properties for topic {TopicName}", topic.Name);
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
            catch (Exception ex) when (IsEmulatorListingNotSupported(ex))
            {
                _logger.LogWarning(ex, "Listing topics not supported by emulator. Falling back to known entities.");
                result = await GetTopicsFromKnownEntitiesAsync(admin, ct);
            }
        }
        catch (Exception ex) when (IsConnectivityError(ex))
        {
            _logger.LogWarning(ex, "Admin client not available. This may be due to emulator port 5300 not being exposed. Returning empty topic list.");
            _logger.LogInformation("Tip: Ensure the Azure Service Bus emulator is running and port 5300 is exposed for management operations.");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve topics from Service Bus. Connection string or endpoint may be incorrect.");
            throw;
        }
        
        return result;
    }

    /// <summary>
    /// Determines if the exception indicates that the emulator doesn't support listing operations.
    /// The emulator returns 404 (MessagingEntityNotFound) for GetQueuesAsync/GetTopicsAsync
    /// because it doesn't implement the /$Resources/Queues enumeration endpoint.
    /// </summary>
    private static bool IsEmulatorListingNotSupported(Exception ex)
    {
        return ex is Azure.Messaging.ServiceBus.ServiceBusException sbEx 
                   && sbEx.Reason == Azure.Messaging.ServiceBus.ServiceBusFailureReason.MessagingEntityNotFound
               || ex is Azure.RequestFailedException rfEx 
                   && rfEx.Status == 404;
    }

    /// <summary>
    /// Returns true only for transient connectivity failures (socket/network errors, HTTP 503/504/408).
    /// Auth and configuration errors (401, 403, 400) are NOT matched — those should propagate so
    /// the UI can surface a meaningful "invalid credentials" or "bad endpoint" error.
    /// </summary>
    private static bool IsConnectivityError(Exception ex)
    {
        if (ex.InnerException is System.Net.Sockets.SocketException or System.IO.IOException)
            return true;

        if (ex is Azure.RequestFailedException rfe)
            return rfe.Status is 503 or 504 or 408 or 0;

        return false;
    }
    
    /// <summary>
    /// Fallback: query each known queue individually using GetQueueRuntimePropertiesAsync.
    /// </summary>
    private async Task<List<QueueInfo>> GetQueuesFromKnownEntitiesAsync(ServiceBusAdministrationClient admin, CancellationToken ct)
    {
        var result = new List<QueueInfo>();
        var knownQueues = _options.KnownQueues?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        
        if (knownQueues.Length == 0)
        {
            _logger.LogInformation("No known queues configured. Set SERVICEBUS_KNOWN_QUEUES environment variable.");
            return result;
        }
        
        foreach (var queueName in knownQueues)
        {
            try
            {
                QueueProperties? props = null;
                QueueRuntimeProperties? runtime = null;
                
                try { props = await admin.GetQueueAsync(queueName, ct); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to get properties for known queue {QueueName}", queueName); }
                
                try { runtime = await admin.GetQueueRuntimePropertiesAsync(queueName, ct); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to get runtime properties for known queue {QueueName}", queueName); }
                
                result.Add(new QueueInfo
                {
                    Name = queueName,
                    ActiveMessageCount = runtime?.ActiveMessageCount ?? 0,
                    DeadLetterMessageCount = runtime?.DeadLetterMessageCount ?? 0,
                    TransferDeadLetterMessageCount = runtime?.TransferDeadLetterMessageCount ?? 0,
                    SizeInBytes = runtime?.SizeInBytes ?? 0,
                    CreatedAt = runtime?.CreatedAt,
                    UpdatedAt = runtime?.UpdatedAt,
                    LockDuration = props?.LockDuration ?? TimeSpan.FromSeconds(30),
                    MaxDeliveryCount = props?.MaxDeliveryCount ?? 10,
                    RequiresSession = props?.RequiresSession ?? false,
                    MaxSizeInMegabytes = props?.MaxSizeInMegabytes ?? 0
                });
                
                _logger.LogInformation("Successfully queried known queue: {QueueName}", queueName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query known queue {QueueName}, adding with defaults.", queueName);
                result.Add(new QueueInfo { Name = queueName });
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Fallback: query each known topic individually.
    /// </summary>
    private async Task<List<TopicInfo>> GetTopicsFromKnownEntitiesAsync(ServiceBusAdministrationClient admin, CancellationToken ct)
    {
        var result = new List<TopicInfo>();
        var knownTopics = _options.KnownTopics?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        
        if (knownTopics.Length == 0)
        {
            _logger.LogInformation("No known topics configured. Set SERVICEBUS_KNOWN_TOPICS environment variable.");
            return result;
        }
        
        // Parse known subscriptions into a lookup: TopicName -> [SubscriptionName, ...]
        var knownSubsByTopic = ParseKnownSubscriptions();
        
        foreach (var topicName in knownTopics)
        {
            try
            {
                TopicProperties? props = null;
                TopicRuntimeProperties? runtime = null;
                
                try { props = await admin.GetTopicAsync(topicName, ct); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to get properties for known topic {TopicName}", topicName); }
                
                try { runtime = await admin.GetTopicRuntimePropertiesAsync(topicName, ct); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to get runtime properties for known topic {TopicName}", topicName); }
                
                // Get subscriptions - try listing first, fall back to known subscriptions
                var subs = await GetSubscriptionsForTopicAsync(admin, topicName, ct);
                if (subs.Count == 0 && knownSubsByTopic.TryGetValue(topicName, out var knownSubNames))
                {
                    subs = await GetSubscriptionsFromKnownEntitiesAsync(admin, topicName, knownSubNames, ct);
                }
                
                result.Add(new TopicInfo
                {
                    Name = topicName,
                    SizeInBytes = runtime?.SizeInBytes ?? 0,
                    CreatedAt = runtime?.CreatedAt,
                    UpdatedAt = runtime?.UpdatedAt,
                    MaxSizeInMegabytes = props?.MaxSizeInMegabytes ?? 0,
                    Subscriptions = subs
                });
                
                _logger.LogInformation("Successfully queried known topic: {TopicName}", topicName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query known topic {TopicName}, adding with defaults.", topicName);
                result.Add(new TopicInfo { Name = topicName, Subscriptions = [] });
            }
        }
        
        return result;
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
                    _logger.LogWarning(ex, "Failed to get runtime properties for subscription {SubscriptionName} on topic {TopicName}", sub.SubscriptionName, topicName);
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
        catch (Exception ex) when (IsEmulatorListingNotSupported(ex))
        {
            _logger.LogWarning("Listing subscriptions for topic {TopicName} not supported by emulator.", topicName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get subscriptions for topic {TopicName}", topicName);
        }
        
        return subs;
    }
    
    private async Task<List<SubscriptionInfo>> GetSubscriptionsFromKnownEntitiesAsync(
        ServiceBusAdministrationClient admin, string topicName, List<string> subscriptionNames, CancellationToken ct)
    {
        var subs = new List<SubscriptionInfo>();
        
        foreach (var subName in subscriptionNames)
        {
            try
            {
                SubscriptionProperties? props = null;
                SubscriptionRuntimeProperties? runtime = null;
                
                try { props = await admin.GetSubscriptionAsync(topicName, subName, ct); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to get properties for known subscription {SubName} on {TopicName}", subName, topicName); }
                
                try { runtime = await admin.GetSubscriptionRuntimePropertiesAsync(topicName, subName, ct); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to get runtime properties for known subscription {SubName} on {TopicName}", subName, topicName); }
                
                subs.Add(new SubscriptionInfo
                {
                    TopicName = topicName,
                    Name = subName,
                    ActiveMessageCount = runtime?.ActiveMessageCount ?? 0,
                    DeadLetterMessageCount = runtime?.DeadLetterMessageCount ?? 0,
                    CreatedAt = runtime?.CreatedAt,
                    UpdatedAt = runtime?.UpdatedAt,
                    LockDuration = props?.LockDuration ?? TimeSpan.FromSeconds(30),
                    MaxDeliveryCount = props?.MaxDeliveryCount ?? 10,
                    RequiresSession = props?.RequiresSession ?? false
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query known subscription {SubName} on {TopicName}, adding with defaults.", subName, topicName);
                subs.Add(new SubscriptionInfo { TopicName = topicName, Name = subName });
            }
        }
        
        return subs;
    }
    
    private Dictionary<string, List<string>> ParseKnownSubscriptions()
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var entries = _options.KnownSubscriptions?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        
        foreach (var entry in entries)
        {
            var parts = entry.Split(':', 2);
            if (parts.Length == 2)
            {
                var topicName = parts[0].Trim();
                var subName = parts[1].Trim();
                if (!result.ContainsKey(topicName))
                    result[topicName] = [];
                result[topicName].Add(subName);
            }
        }
        
        return result;
    }
}
