using Microsoft.AspNetCore.Components;

namespace PicoBusX.Web.Components;

/// <summary>
/// Abstract base class for message panel components (<see cref="PeekReadPanel"/> and <see cref="DlqPanel"/>).
/// Provides shared state management for max message count, expand/collapse tracking,
/// and utility formatting used by both panels.
/// </summary>
public abstract class MessagePanelBase : ComponentBase
{
    [Parameter] public int DefaultMaxCount { get; set; } = 10;

    protected int _maxCount;
    protected HashSet<long> _expanded = new();

    protected override void OnInitialized()
    {
        _maxCount = DefaultMaxCount > 0 ? DefaultMaxCount : 10;
    }

    protected override void OnParametersSet()
    {
        if (_maxCount == 0) _maxCount = DefaultMaxCount > 0 ? DefaultMaxCount : 10;
    }

    protected void ToggleMessage(long sequenceNumber)
    {
        if (_expanded.Contains(sequenceNumber)) _expanded.Remove(sequenceNumber);
        else _expanded.Add(sequenceNumber);
    }

    protected static string PrettyPrint(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "(empty)";
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            return System.Text.Json.JsonSerializer.Serialize(doc.RootElement, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch (System.Text.Json.JsonException)
        {
            return body;
        }
    }
}
