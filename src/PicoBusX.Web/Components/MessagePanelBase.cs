using Microsoft.AspNetCore.Components;
using PicoBusX.Web.Models;

namespace PicoBusX.Web.Components;

/// <summary>
/// Abstract base class for message panel components (<see cref="PeekReadPanel"/> and <see cref="DlqPanel"/>).
/// Provides shared parameters, state management for max message count, expand/collapse tracking,
/// client-side message filtering, and utility formatting used by both panels.
/// </summary>
public abstract class MessagePanelBase : ComponentBase
{
    [Parameter] public int DefaultMaxCount { get; set; } = 10;
    [Parameter] public string EntityPath { get; set; } = string.Empty;
    [Parameter] public EventCallback<(string entityPath, int maxCount, long? fromSequenceNumber)> OnPeek { get; set; }
    [Parameter] public List<BrowsedMessage> Messages { get; set; } = new();
    [Parameter] public bool IsBusy { get; set; }
    [Parameter] public bool HasMore { get; set; }
    [Parameter] public string? ErrorMessage { get; set; }

    protected int _maxCount;
    protected HashSet<long> _expanded = new();
    protected string _filterText = string.Empty;

    protected override void OnInitialized()
    {
        _maxCount = DefaultMaxCount > 0 ? DefaultMaxCount : 10;
    }

    protected override void OnParametersSet()
    {
        if (_maxCount == 0) _maxCount = DefaultMaxCount > 0 ? DefaultMaxCount : 10;
    }

    protected virtual async Task DoPeek() =>
        await OnPeek.InvokeAsync((EntityPath, _maxCount, null));

    protected virtual async Task DoLoadMore()
    {
        var fromSeq = Messages.Count > 0 ? Messages.Max(m => m.SequenceNumber) + 1 : (long?)null;
        await OnPeek.InvokeAsync((EntityPath, _maxCount, fromSeq));
    }

    protected void ToggleMessage(long sequenceNumber)
    {
        if (_expanded.Contains(sequenceNumber)) _expanded.Remove(sequenceNumber);
        else _expanded.Add(sequenceNumber);
    }

    /// <summary>
    /// Returns the subset of <paramref name="messages"/> that match the current
    /// <see cref="_filterText"/> (case-insensitive substring search across
    /// MessageId, Subject, CorrelationId, SessionId, Body, and ApplicationProperties).
    /// Returns all messages when the filter is empty.
    /// </summary>
    protected IReadOnlyList<BrowsedMessage> FilterMessages(List<BrowsedMessage> messages)
    {
        if (string.IsNullOrWhiteSpace(_filterText))
            return messages;

        var term = _filterText.Trim();
        return messages.Where(m =>
            ContainsIgnoreCase(m.MessageId, term) ||
            ContainsIgnoreCase(m.Subject, term) ||
            ContainsIgnoreCase(m.CorrelationId, term) ||
            ContainsIgnoreCase(m.SessionId, term) ||
            ContainsIgnoreCase(m.Body, term) ||
            m.ApplicationProperties.Any(kv =>
                ContainsIgnoreCase(kv.Key, term) || ContainsIgnoreCase(kv.Value, term))
        ).ToList();
    }

    private static bool ContainsIgnoreCase(string? value, string term) =>
        !string.IsNullOrEmpty(value) && value.Contains(term, StringComparison.OrdinalIgnoreCase);

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
