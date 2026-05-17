using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using PicoBusX.Web.Components;
using PicoBusX.Web.Models;

namespace PicoBusX.Web.Tests.Components;

public class DlqPanelTests : TestContext
{
    public DlqPanelTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
    }

    [Fact]
    public async Task DoBulkResubmit_WithSelectedMessages_InvokesCallbackAndClearsSelection()
    {
        IReadOnlyList<long>? captured = null;
        var cut = RenderPanel(sequenceNumbers: [101, 102], onBulkResubmit: selected => captured = selected);

        // Select all visible messages via the "Select Visible" toolbar button
        await ClickButtonContaining(cut, "Select Visible");

        // Trigger bulk resubmit
        await ClickButtonContaining(cut, "Resubmit Selected");

        captured.Should().BeEquivalentTo([101L, 102L]);

        // After resubmit, selection is cleared; a second attempt should not invoke the callback
        captured = null;
        await ClickButtonContaining(cut, "Resubmit Selected");
        captured.Should().BeNull();
    }

    [Fact]
    public async Task OnParametersSet_WhenMessagesChanged_RemovesStaleSelections()
    {
        IReadOnlyList<long>? captured = null;
        var cut = RenderPanel(sequenceNumbers: [101, 102], onBulkRemove: selected => captured = selected);

        // Select all visible messages (101 and 102)
        await ClickButtonContaining(cut, "Select Visible");

        // Reduce message list to only 102 — stale selection for 101 should be pruned
        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.Messages, CreateMessages([102])));

        // Trigger bulk remove — only 102 should remain in the selection
        await ClickButtonContaining(cut, "Remove Selected");

        captured.Should().BeEquivalentTo([102L]);
    }

    private static async Task ClickButtonContaining(IRenderedComponent<DlqPanel> cut, string text)
    {
        var button = cut.FindComponents<FluentButton>()
            .First(b => b.Markup.Contains(text));
        await cut.InvokeAsync(() => button.Instance.OnClick.InvokeAsync(new MouseEventArgs()));
    }

    private IRenderedComponent<DlqPanel> RenderPanel(
        IReadOnlyList<long> sequenceNumbers,
        Action<IReadOnlyList<long>>? onBulkResubmit = null,
        Action<IReadOnlyList<long>>? onBulkRemove = null)
    {
        return RenderComponent<DlqPanel>(parameters => parameters
            .Add(p => p.EntityPath, "orders")
            .Add(p => p.Messages, CreateMessages(sequenceNumbers))
            .Add(p => p.OnBulkResubmit, EventCallback.Factory.Create<IReadOnlyList<long>>(this, selected => onBulkResubmit?.Invoke(selected)))
            .Add(p => p.OnBulkRemove, EventCallback.Factory.Create<IReadOnlyList<long>>(this, selected => onBulkRemove?.Invoke(selected))));
    }

    private static List<BrowsedMessage> CreateMessages(IReadOnlyList<long> sequenceNumbers) =>
        sequenceNumbers.Select(sequenceNumber => new BrowsedMessage
        {
            SequenceNumber = sequenceNumber,
            MessageId = $"msg-{sequenceNumber}",
            Body = "{}",
            EnqueuedTime = DateTimeOffset.UtcNow
        }).ToList();
}
