using PicoBusX.Web.Models;

namespace PicoBusX.Web.Services;

public interface IExplorerService
{
    Task<ExplorerLoadResult> LoadAsync(CancellationToken ct = default);
    Task<List<QueueInfo>> GetQueuesAsync(CancellationToken ct = default);
    Task<List<TopicInfo>> GetTopicsAsync(CancellationToken ct = default);
}

