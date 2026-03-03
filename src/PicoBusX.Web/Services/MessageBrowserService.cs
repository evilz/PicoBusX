using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using PicoBusX.Web.Models;
using PicoBusX.Web.Options;

namespace PicoBusX.Web.Services;

public class MessageBrowserService
{
    private readonly ServiceBusClientFactory _factory;
    private readonly ServiceBusConnectionOptions _options;
    private readonly ILogger<MessageBrowserService> _logger;

    public MessageBrowserService(ServiceBusClientFactory factory, IOptions<ServiceBusConnectionOptions> options, ILogger<MessageBrowserService> logger)
    {
        _factory = factory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<List<BrowsedMessage>> PeekMessagesAsync(string entityPath, int maxCount, CancellationToken ct = default)
    {
        var client = _factory.GetClient();
        await using var receiver = client.CreateReceiver(entityPath);
        var peeked = await receiver.PeekMessagesAsync(maxCount, cancellationToken: ct);
        return peeked.Select(MapMessage).ToList();
    }

    /// <summary>
    /// Receives messages in PeekLock mode, maps them, then abandons all locks so they remain in the queue.
    /// </summary>
    public async Task<List<BrowsedMessage>> ReceiveAndAbandonAsync(string entityPath, int maxCount, CancellationToken ct = default)
    {
        var client = _factory.GetClient();
        await using var receiver = client.CreateReceiver(entityPath, new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });
        var messages = await receiver.ReceiveMessagesAsync(maxCount, maxWaitTime: TimeSpan.FromSeconds(5), cancellationToken: ct);
        var result = messages.Select(MapMessage).ToList();
        // Abandon all locks so messages are returned to the queue immediately.
        foreach (var m in messages)
        {
            try { await receiver.AbandonMessageAsync(m, cancellationToken: ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to abandon message {MessageId}", m.MessageId); }
        }
        return result;
    }

    private BrowsedMessage MapMessage(ServiceBusReceivedMessage m)
    {
        string body = string.Empty;
        try { body = m.Body?.ToString() ?? string.Empty; }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read body of message {MessageId}", m.MessageId); body = "(error reading body)"; }
        return new BrowsedMessage
        {
            MessageId = m.MessageId,
            Subject = m.Subject,
            SessionId = m.SessionId,
            CorrelationId = m.CorrelationId,
            ContentType = m.ContentType,
            EnqueuedTime = m.EnqueuedTime,
            DeliveryCount = m.DeliveryCount,
            Body = body,
            ApplicationProperties = m.ApplicationProperties.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? string.Empty)
        };
    }
}
