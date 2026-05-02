namespace PicoBusX.Web.Models;

public interface IQueueLikeEntity
{
    TimeSpan LockDuration { get; }
    int MaxDeliveryCount { get; }
    bool RequiresSession { get; }
    TimeSpan DefaultMessageTimeToLive { get; }
    TimeSpan AutoDeleteOnIdle { get; }
    bool EnableBatchedOperations { get; }
    string? ForwardTo { get; }
    string? ForwardDeadLetteredMessagesTo { get; }
    bool DeadLetteringOnMessageExpiration { get; }
    string Status { get; }
    DateTimeOffset? CreatedAt { get; }
    DateTimeOffset? UpdatedAt { get; }
    DateTimeOffset? AccessedAt { get; }
}
