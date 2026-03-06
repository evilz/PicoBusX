using Azure.Messaging.ServiceBus;
using PicoBusX.Web.Models;

namespace PicoBusX.Web.Services;

public class MessageBrowserService
{
    private readonly ServiceBusClientFactory _factory;
    private readonly ILogger<MessageBrowserService> _logger;

    public MessageBrowserService(ServiceBusClientFactory factory, ILogger<MessageBrowserService> logger)
    {
        _factory = factory;
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

    public async Task<List<BrowsedMessage>> PeekDeadLetterAsync(string entityPath, int maxCount, CancellationToken ct = default)
    {
        var client = _factory.GetClient();
        await using var receiver = client.CreateReceiver(entityPath, new ServiceBusReceiverOptions
        {
            SubQueue = SubQueue.DeadLetter,
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });
        var peeked = await receiver.PeekMessagesAsync(maxCount, cancellationToken: ct);
        return peeked.Select(MapMessage).ToList();
    }

    public async Task ResubmitDeadLetterAsync(string entityPath, long sequenceNumber, CancellationToken ct = default)
    {
        var client = _factory.GetClient();
        await using var receiver = client.CreateReceiver(entityPath, new ServiceBusReceiverOptions
        {
            SubQueue = SubQueue.DeadLetter,
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });
        var messages = await receiver.ReceiveMessagesAsync(maxMessages: 100, maxWaitTime: TimeSpan.FromSeconds(5), cancellationToken: ct);
        var target = messages.FirstOrDefault(m => m.SequenceNumber == sequenceNumber);
        if (target == null)
        {
            foreach (var m in messages)
            {
                try { await receiver.AbandonMessageAsync(m, cancellationToken: ct); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to abandon DLQ message {MessageId} during not-found cleanup", m.MessageId); }
            }
            throw new InvalidOperationException($"Message with sequence number {sequenceNumber} not found in DLQ.");
        }

        string resendTo = entityPath;
        if (entityPath.Contains("/subscriptions/"))
            resendTo = entityPath.Split("/subscriptions/")[0];

        var newMessage = new ServiceBusMessage(target.Body)
        {
            ContentType = target.ContentType
        };
        if (!string.IsNullOrEmpty(target.Subject)) newMessage.Subject = target.Subject;
        if (!string.IsNullOrEmpty(target.CorrelationId)) newMessage.CorrelationId = target.CorrelationId;
        if (!string.IsNullOrEmpty(target.SessionId)) newMessage.SessionId = target.SessionId;
        foreach (var kv in target.ApplicationProperties)
            newMessage.ApplicationProperties[kv.Key] = kv.Value;

        await using var sender = client.CreateSender(resendTo);
        await sender.SendMessageAsync(newMessage, ct);
        await receiver.CompleteMessageAsync(target, ct);

        foreach (var m in messages.Where(m => m.SequenceNumber != sequenceNumber))
        {
            try { await receiver.AbandonMessageAsync(m, cancellationToken: ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to abandon DLQ message {MessageId} after resubmit", m.MessageId); }
        }

        _logger.LogInformation("Resubmitted message {SequenceNumber} from DLQ {EntityPath} to {ResendTo}", sequenceNumber, entityPath, resendTo);
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
            ApplicationProperties = m.ApplicationProperties.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? string.Empty),
            SequenceNumber = m.SequenceNumber,
            DeadLetterReason = m.DeadLetterReason,
            DeadLetterErrorDescription = m.DeadLetterErrorDescription
        };
    }
}
