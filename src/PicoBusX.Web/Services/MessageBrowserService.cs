using Azure.Messaging.ServiceBus;
using PicoBusX.Web.Models;

namespace PicoBusX.Web.Services;

public class MessageBrowserService
{
    private const int SessionAcceptTimeoutSeconds = 3;
    private const int SessionReceiveWaitSeconds = 5;

    private readonly ServiceBusClientFactory _factory;
    private readonly ILogger<MessageBrowserService> _logger;

    public MessageBrowserService(ServiceBusClientFactory factory, ILogger<MessageBrowserService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<List<BrowsedMessage>> PeekMessagesAsync(string entityPath, int maxCount, long? fromSequenceNumber = null, CancellationToken ct = default)
    {
        var client = _factory.GetClient();
        await using var receiver = client.CreateReceiver(entityPath);
        var peeked = await receiver.PeekMessagesAsync(maxCount, fromSequenceNumber, cancellationToken: ct);
        return peeked.Select(m => MapMessage(m, _logger)).ToList();
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
        var result = messages.Select(m => MapMessage(m, _logger)).ToList();
        // Abandon all locks so messages are returned to the queue immediately.
        foreach (var m in messages)
        {
            try { await receiver.AbandonMessageAsync(m, cancellationToken: ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to abandon message {MessageId}", m.MessageId); }
        }
        return result;
    }

    /// <summary>
    /// Peeks messages from a session-enabled entity by sampling across multiple sessions.
    /// Iterates available sessions (with a per-session timeout) until <paramref name="maxCount"/>
    /// messages are collected or no new sessions remain.
    /// </summary>
    public static Task<List<BrowsedMessage>> PeekSessionMessagesAsync(ServiceBusClient client, string entityPath, int maxCount, CancellationToken ct = default)
        => IterateSessionsAsync(client, entityPath, maxCount, ct, async (sessionReceiver, remaining) =>
        {
            var peeked = await sessionReceiver.PeekMessagesAsync(remaining, cancellationToken: ct);
            return peeked.Select(m => MapMessage(m)).ToList();
        });

    /// <summary>
    /// Receives messages in PeekLock mode from a session-enabled entity, maps them,
    /// then abandons all locks so they remain in the queue.
    /// Iterates available sessions until <paramref name="maxCount"/> messages are collected
    /// or no new sessions remain.
    /// </summary>
    public Task<List<BrowsedMessage>> ReceiveAndAbandonSessionAsync(string entityPath, int maxCount, CancellationToken ct = default)
        => IterateSessionsAsync(_factory.GetClient(), entityPath, maxCount, ct, async (sessionReceiver, remaining) =>
        {
            var messages = await sessionReceiver.ReceiveMessagesAsync(remaining, maxWaitTime: TimeSpan.FromSeconds(SessionReceiveWaitSeconds), cancellationToken: ct);
            var result = messages.Select(m => MapMessage(m, _logger)).ToList();
            foreach (var m in messages)
            {
                try { await sessionReceiver.AbandonMessageAsync(m, cancellationToken: ct); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to abandon session message {MessageId}", m.MessageId); }
            }
            return result;
        });

    /// <summary>
    /// Iterates active sessions one at a time, calling <paramref name="processSession"/> for each,
    /// until <paramref name="maxCount"/> messages are collected or no further sessions are available.
    /// </summary>
    private static async Task<List<BrowsedMessage>> IterateSessionsAsync(
        ServiceBusClient client,
        string entityPath,
        int maxCount,
        CancellationToken ct,
        Func<ServiceBusSessionReceiver, int, Task<List<BrowsedMessage>>> processSession)
    {
        var results = new List<BrowsedMessage>();
        var seenSessions = new HashSet<string>();

        while (results.Count < maxCount)
        {
            using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            sessionCts.CancelAfter(TimeSpan.FromSeconds(SessionAcceptTimeoutSeconds));

            ServiceBusSessionReceiver sessionReceiver;
            try
            {
                sessionReceiver = await client.AcceptNextSessionAsync(entityPath, cancellationToken: sessionCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                break; // Timed out — no more sessions available
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceTimeout)
            {
                break;
            }

            await using (sessionReceiver)
            {
                if (!seenSessions.Add(sessionReceiver.SessionId))
                    break; // Cycled back to an already-visited session

                var batch = await processSession(sessionReceiver, maxCount - results.Count);
                results.AddRange(batch);
            }
        }

        return results;
    }

    public async Task<List<BrowsedMessage>> PeekDeadLetterAsync(string entityPath, int maxCount, long? fromSequenceNumber = null, CancellationToken ct = default)
    {
        var client = _factory.GetClient();
        await using var receiver = client.CreateReceiver(entityPath, new ServiceBusReceiverOptions
        {
            SubQueue = SubQueue.DeadLetter,
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });
        var peeked = await receiver.PeekMessagesAsync(maxCount, fromSequenceNumber, cancellationToken: ct);
        return peeked.Select(m => MapMessage(m, _logger)).ToList();
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
            ContentType = target.ContentType,
            MessageId = target.MessageId
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

    private static BrowsedMessage MapMessage(ServiceBusReceivedMessage m, ILogger? logger = null)
    {
        string body = string.Empty;
        try { body = m.Body?.ToString() ?? string.Empty; }
        catch (Exception ex) { logger?.LogWarning(ex, "Failed to read body of message {MessageId}", m.MessageId); body = "(error reading body)"; }
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
