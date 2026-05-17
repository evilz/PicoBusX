using Azure.Messaging.ServiceBus;
using PicoBusX.Web.Models;

namespace PicoBusX.Web.Services;

public enum MessageSettlementAction
{
    Complete,
    Abandon,
    Defer,
    DeadLetter
}

public class MessageBrowserService : IAsyncDisposable
{
    private const int SessionAcceptTimeoutSeconds = 3;
    private const int ReceiveWaitSeconds = 5;
    private const int ScheduledPeekChunkSize = 50;
    private const int ScheduledPeekMaxScanFactor = 20;
    private const int ScheduledPeekMaxScanCeiling = 2000;
    private const int DlqBulkReceiveBatchSize = 100;
    private const string ManualDeadLetterReason = "ManualDeadLetter";
    private const string ManualDeadLetterDescription = "Moved to DLQ from PicoBusX message browser.";

    private readonly ServiceBusClientFactory _factory;
    private readonly ILogger<MessageBrowserService> _logger;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly Dictionary<string, LockedMessageContext> _lockedMessages = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<ServiceBusReceiver>> _receiversByEntity = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ServiceBusReceiver, int> _receiverMessageCounts = [];

    private sealed record LockedMessageContext(string EntityPath, ServiceBusReceiver Receiver, ServiceBusReceivedMessage Message);

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

    public async Task<List<BrowsedMessage>> PeekScheduledMessagesAsync(string entityPath, int maxCount, long? fromSequenceNumber = null, CancellationToken ct = default)
    {
        if (maxCount <= 0)
        {
            return [];
        }

        var client = _factory.GetClient();
        await using var receiver = client.CreateReceiver(entityPath);

        var results = new List<BrowsedMessage>();
        long? nextSequenceNumber = fromSequenceNumber;
        var scannedMessages = 0;
        var desiredMaxScannedMessages = checked((long)maxCount * ScheduledPeekMaxScanFactor);
        var minScannedMessages = Math.Min(Math.Max(maxCount, 1), ScheduledPeekMaxScanCeiling);
        var maxScannedMessages = (int)Math.Clamp(desiredMaxScannedMessages, minScannedMessages, ScheduledPeekMaxScanCeiling);

        while (results.Count < maxCount && scannedMessages < maxScannedMessages)
        {
            var batchSize = Math.Min(ScheduledPeekChunkSize, maxScannedMessages - scannedMessages);
            var peeked = await receiver.PeekMessagesAsync(batchSize, nextSequenceNumber, cancellationToken: ct);
            if (peeked.Count == 0)
            {
                break;
            }

            scannedMessages += peeked.Count;

            foreach (var message in peeked)
            {
                if (!IsScheduledMessage(message))
                {
                    continue;
                }

                results.Add(MapMessage(message, _logger));
                if (results.Count >= maxCount)
                {
                    break;
                }
            }

            nextSequenceNumber = peeked[^1].SequenceNumber + 1;
        }

        return results;
    }

    public async Task CancelScheduledMessageAsync(string entityPath, long sequenceNumber, CancellationToken ct = default)
    {
        if (sequenceNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequenceNumber), "Sequence number must be greater than zero.");
        }

        var senderPath = entityPath;
        var subscriptionSegmentIndex = entityPath.IndexOf("/subscriptions/", StringComparison.OrdinalIgnoreCase);
        if (subscriptionSegmentIndex >= 0)
        {
            senderPath = entityPath[..subscriptionSegmentIndex];
        }

        var client = _factory.GetClient();
        await using var sender = client.CreateSender(senderPath);
        await sender.CancelScheduledMessageAsync(sequenceNumber, ct);
    }

    public async Task<List<BrowsedMessage>> ReceiveWithLockAsync(string entityPath, int maxCount, CancellationToken ct = default)
    {
        await ResetPendingMessagesAsync(entityPath, ct);

        var client = _factory.GetClient();
        var receiver = client.CreateReceiver(entityPath, new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });

        try
        {
            var messages = await receiver.ReceiveMessagesAsync(maxCount, maxWaitTime: TimeSpan.FromSeconds(ReceiveWaitSeconds), cancellationToken: ct);
            if (messages.Count == 0)
            {
                await receiver.DisposeAsync();
                return [];
            }

            await TrackLockedMessagesAsync(entityPath, receiver, messages, ct);
            return messages.Select(m => MapMessage(m, _logger, includeLockToken: true, receiverEntityPath: entityPath)).ToList();
        }
        catch
        {
            await receiver.DisposeAsync();
            throw;
        }
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

    public async Task<List<BrowsedMessage>> ReceiveSessionWithLockAsync(string entityPath, int maxCount, CancellationToken ct = default)
    {
        await ResetPendingMessagesAsync(entityPath, ct);

        var results = new List<BrowsedMessage>();
        var seenSessions = new HashSet<string>();
        var client = _factory.GetClient();

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
                break;
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceTimeout)
            {
                break;
            }

            if (!seenSessions.Add(sessionReceiver.SessionId))
            {
                await sessionReceiver.DisposeAsync();
                break;
            }

            try
            {
                var remaining = maxCount - results.Count;
                var messages = await sessionReceiver.ReceiveMessagesAsync(remaining, maxWaitTime: TimeSpan.FromSeconds(ReceiveWaitSeconds), cancellationToken: ct);
                if (messages.Count == 0)
                {
                    await sessionReceiver.DisposeAsync();
                    continue;
                }

                await TrackLockedMessagesAsync(entityPath, sessionReceiver, messages, ct);
                results.AddRange(messages.Select(m => MapMessage(m, _logger, includeLockToken: true, receiverEntityPath: entityPath)));
            }
            catch (Exception)
            {
                await sessionReceiver.DisposeAsync();
                throw;
            }
        }

        return results;
    }

    public async Task<bool> SettleReceivedMessageAsync(string lockToken, MessageSettlementAction action, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(lockToken))
        {
            return false;
        }

        var context = await GetLockedMessageContextAsync(lockToken, ct);
        if (context is null)
        {
            return false;
        }

        var removeTrackedMessage = false;
        try
        {
            switch (action)
            {
                case MessageSettlementAction.Complete:
                    await context.Receiver.CompleteMessageAsync(context.Message, ct);
                    break;
                case MessageSettlementAction.Abandon:
                    await context.Receiver.AbandonMessageAsync(context.Message, cancellationToken: ct);
                    break;
                case MessageSettlementAction.Defer:
                    await context.Receiver.DeferMessageAsync(context.Message, cancellationToken: ct);
                    break;
                case MessageSettlementAction.DeadLetter:
                    await context.Receiver.DeadLetterMessageAsync(context.Message, ManualDeadLetterReason, ManualDeadLetterDescription, ct);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported settlement action.");
            }

            removeTrackedMessage = true;
            return true;
        }
        catch (ServiceBusException ex) when (ex.Reason is ServiceBusFailureReason.MessageLockLost or ServiceBusFailureReason.SessionLockLost)
        {
            _logger.LogWarning(ex, "Message lock expired before settlement for token {LockToken}", lockToken);
            removeTrackedMessage = true;
            return false;
        }
        finally
        {
            if (removeTrackedMessage)
            {
                await RemoveTrackedMessageAsync(lockToken, context, ct);
            }
        }
    }

    public async Task ResetPendingMessagesAsync(string entityPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(entityPath))
        {
            return;
        }

        List<KeyValuePair<string, LockedMessageContext>> contexts;
        List<ServiceBusReceiver> receivers;

        await _stateLock.WaitAsync(ct);
        try
        {
            contexts = _lockedMessages
                .Where(kv => kv.Value.EntityPath.Equals(entityPath, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var context in contexts)
            {
                _lockedMessages.Remove(context.Key);
            }

            if (_receiversByEntity.TryGetValue(entityPath, out var trackedReceivers))
            {
                receivers = trackedReceivers.ToList();
                _receiversByEntity.Remove(entityPath);

                foreach (var receiver in receivers)
                {
                    _receiverMessageCounts.Remove(receiver);
                }
            }
            else
            {
                receivers = [];
            }
        }
        finally
        {
            _stateLock.Release();
        }

        foreach (var context in contexts)
        {
            await TryAbandonAsync(context.Value.Receiver, context.Value.Message, "reset-pending", ct);
        }

        foreach (var receiver in receivers)
        {
            await receiver.DisposeAsync();
        }
    }

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
        var resubmitted = await ResubmitDeadLettersAsync(entityPath, [sequenceNumber], ct);
        if (!resubmitted.Contains(sequenceNumber))
        {
            throw new InvalidOperationException($"Message with sequence number {sequenceNumber} not found in DLQ.");
        }
    }

    public Task<IReadOnlyList<long>> ResubmitDeadLettersAsync(string entityPath, IReadOnlyCollection<long> sequenceNumbers, CancellationToken ct = default) =>
        ProcessDeadLettersAsync(entityPath, sequenceNumbers, shouldResubmit: true, ct);

    public Task<IReadOnlyList<long>> RemoveDeadLettersAsync(string entityPath, IReadOnlyCollection<long> sequenceNumbers, CancellationToken ct = default) =>
        ProcessDeadLettersAsync(entityPath, sequenceNumbers, shouldResubmit: false, ct);

    private async Task<IReadOnlyList<long>> ProcessDeadLettersAsync(
        string entityPath,
        IReadOnlyCollection<long> sequenceNumbers,
        bool shouldResubmit,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityPath);
        ArgumentNullException.ThrowIfNull(sequenceNumbers);

        var invalidSequenceNumbers = sequenceNumbers.Where(sequenceNumber => sequenceNumber <= 0).Distinct().ToList();
        if (invalidSequenceNumbers.Count > 0)
        {
            _logger.LogWarning(
                "Ignoring invalid DLQ sequence numbers for {EntityPath}: {SequenceNumbers}",
                entityPath,
                string.Join(", ", invalidSequenceNumbers));
        }

        var targets = sequenceNumbers.Where(sequenceNumber => sequenceNumber > 0).Distinct().ToHashSet();
        if (targets.Count == 0)
        {
            return [];
        }

        var client = _factory.GetClient();
        await using var receiver = client.CreateReceiver(entityPath, new ServiceBusReceiverOptions
        {
            SubQueue = SubQueue.DeadLetter,
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });

        ServiceBusSender? sender = null;
        if (shouldResubmit)
        {
            sender = client.CreateSender(GetResubmitEntityPath(entityPath));
        }

        var pending = targets.ToHashSet();
        var processed = new List<long>(targets.Count);
        var pendingAbandon = new List<ServiceBusReceivedMessage>();

        try
        {
            while (pending.Count > 0)
            {
                var messages = await receiver.ReceiveMessagesAsync(maxMessages: DlqBulkReceiveBatchSize, maxWaitTime: TimeSpan.FromSeconds(ReceiveWaitSeconds), cancellationToken: ct);
                if (messages.Count == 0)
                {
                    break;
                }

                foreach (var message in messages)
                {
                    if (!pending.Contains(message.SequenceNumber))
                    {
                        pendingAbandon.Add(message);
                        continue;
                    }

                    try
                    {
                        if (shouldResubmit)
                        {
                            if (sender is null)
                            {
                                throw new InvalidOperationException("Resubmit sender was not initialized.");
                            }

                            await sender.SendMessageAsync(CloneDeadLetterMessage(message), ct);
                        }

                        await receiver.CompleteMessageAsync(message, ct);
                        pending.Remove(message.SequenceNumber);
                        processed.Add(message.SequenceNumber);
                    }
                    catch
                    {
                        pendingAbandon.Add(message);
                        throw;
                    }
                }
            }
        }
        finally
        {
            foreach (var message in pendingAbandon)
            {
                await TryAbandonAsync(receiver, message, "dlq-bulk-cleanup", ct);
            }

            if (sender is not null)
            {
                await sender.DisposeAsync();
            }
        }

        if (shouldResubmit)
        {
            _logger.LogInformation(
                "Resubmitted {ProcessedCount} of {RequestedCount} message(s) from DLQ {EntityPath} to {ResendTo}",
                processed.Count,
                targets.Count,
                entityPath,
                GetResubmitEntityPath(entityPath));
        }
        else
        {
            _logger.LogInformation(
                "Removed {ProcessedCount} of {RequestedCount} message(s) from DLQ {EntityPath}",
                processed.Count,
                targets.Count,
                entityPath);
        }

        return processed;
    }

    private static ServiceBusMessage CloneDeadLetterMessage(ServiceBusReceivedMessage source)
    {
        var message = new ServiceBusMessage(source.Body)
        {
            ContentType = source.ContentType,
            MessageId = source.MessageId
        };

        if (!string.IsNullOrEmpty(source.Subject)) message.Subject = source.Subject;
        if (!string.IsNullOrEmpty(source.CorrelationId)) message.CorrelationId = source.CorrelationId;
        if (!string.IsNullOrEmpty(source.SessionId)) message.SessionId = source.SessionId;
        foreach (var kv in source.ApplicationProperties)
            message.ApplicationProperties[kv.Key] = kv.Value;

        return message;
    }

    private static string GetResubmitEntityPath(string entityPath)
    {
        var subscriptionSegmentIndex = entityPath.IndexOf("/subscriptions/", StringComparison.OrdinalIgnoreCase);
        return subscriptionSegmentIndex >= 0 ? entityPath[..subscriptionSegmentIndex] : entityPath;
    }

    private async Task TryAbandonAsync(ServiceBusReceiver receiver, ServiceBusReceivedMessage message, string context, CancellationToken ct)
    {
        try { await receiver.AbandonMessageAsync(message, cancellationToken: ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to abandon message {MessageId} ({Context})", message.MessageId, context); }
    }

    private async Task<LockedMessageContext?> GetLockedMessageContextAsync(string lockToken, CancellationToken ct)
    {
        await _stateLock.WaitAsync(ct);
        try
        {
            return _lockedMessages.GetValueOrDefault(lockToken);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task TrackLockedMessagesAsync(string entityPath, ServiceBusReceiver receiver, IReadOnlyList<ServiceBusReceivedMessage> messages, CancellationToken ct)
    {
        await _stateLock.WaitAsync(ct);
        try
        {
            if (!_receiversByEntity.TryGetValue(entityPath, out var receivers))
            {
                receivers = [];
                _receiversByEntity[entityPath] = receivers;
            }

            receivers.Add(receiver);
            _receiverMessageCounts.TryGetValue(receiver, out var existingCount);
            _receiverMessageCounts[receiver] = existingCount + messages.Count;

            foreach (var message in messages)
            {
                _lockedMessages[message.LockToken] = new LockedMessageContext(entityPath, receiver, message);
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task RemoveTrackedMessageAsync(string lockToken, LockedMessageContext context, CancellationToken ct)
    {
        var disposeReceiver = false;

        await _stateLock.WaitAsync(ct);
        try
        {
            _lockedMessages.Remove(lockToken);

            if (_receiverMessageCounts.TryGetValue(context.Receiver, out var messageCount))
            {
                messageCount--;
                if (messageCount <= 0)
                {
                    _receiverMessageCounts.Remove(context.Receiver);
                    if (_receiversByEntity.TryGetValue(context.EntityPath, out var receivers))
                    {
                        receivers.Remove(context.Receiver);
                        if (receivers.Count == 0)
                        {
                            _receiversByEntity.Remove(context.EntityPath);
                        }
                    }

                    disposeReceiver = true;
                }
                else
                {
                    _receiverMessageCounts[context.Receiver] = messageCount;
                }
            }
        }
        finally
        {
            _stateLock.Release();
        }

        if (disposeReceiver)
        {
            await context.Receiver.DisposeAsync();
        }
    }

    private static BrowsedMessage MapMessage(ServiceBusReceivedMessage m, ILogger? logger = null, bool includeLockToken = false, string? receiverEntityPath = null)
    {
        string body = string.Empty;
        try { body = m.Body?.ToString() ?? string.Empty; }
        catch (Exception ex) { logger?.LogWarning(ex, "Failed to read body of message {MessageId}", m.MessageId); body = "(error reading body)"; }
        var scheduledEnqueueTime = m.ScheduledEnqueueTime <= DateTimeOffset.MinValue ? (DateTimeOffset?)null : m.ScheduledEnqueueTime;
        var isScheduled = IsScheduledMessage(m);

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
            LockToken = includeLockToken ? m.LockToken : null,
            ReceiverEntityPath = receiverEntityPath,
            SequenceNumber = m.SequenceNumber,
            DeadLetterReason = m.DeadLetterReason,
            DeadLetterErrorDescription = m.DeadLetterErrorDescription,
            IsScheduled = isScheduled,
            ScheduledEnqueueTime = scheduledEnqueueTime
        };
    }

    private static bool IsScheduledMessage(ServiceBusReceivedMessage message)
    {
        if (message.State == ServiceBusMessageState.Scheduled)
        {
            return true;
        }

        return message.ScheduledEnqueueTime > DateTimeOffset.UtcNow;
    }

    public async ValueTask DisposeAsync()
    {
        List<ServiceBusReceiver> receivers;

        await _stateLock.WaitAsync();
        try
        {
            receivers = _receiversByEntity.Values.SelectMany(v => v).Distinct().ToList();
            _lockedMessages.Clear();
            _receiversByEntity.Clear();
            _receiverMessageCounts.Clear();
        }
        finally
        {
            _stateLock.Release();
        }

        foreach (var receiver in receivers)
        {
            await receiver.DisposeAsync();
        }

        _stateLock.Dispose();
    }
}
