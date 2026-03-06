namespace PicoBusX.Web.Models;

public class ExplorerLoadResult
{
    public List<QueueInfo> Queues { get; init; } = new();
    public List<TopicInfo> Topics { get; init; } = new();
    public string? WarningMessage { get; init; }
    public string? ErrorMessage { get; init; }

    public bool HasWarning => !string.IsNullOrWhiteSpace(WarningMessage);
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
}

