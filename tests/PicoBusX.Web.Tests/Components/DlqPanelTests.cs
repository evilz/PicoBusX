using System.Reflection;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
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

        InvokePrivateMethod(cut.Instance, "SetSelected", 101L, true);
        InvokePrivateMethod(cut.Instance, "SetSelected", 102L, true);

        await cut.InvokeAsync(() => InvokePrivateTask(cut.Instance, "DoBulkResubmit"));

        captured.Should().BeEquivalentTo([101L, 102L]);

        captured = null;
        await cut.InvokeAsync(() => InvokePrivateTask(cut.Instance, "DoBulkResubmit"));
        captured.Should().BeNull();
    }

    [Fact]
    public async Task OnParametersSet_WhenMessagesChanged_RemovesStaleSelections()
    {
        IReadOnlyList<long>? captured = null;
        var cut = RenderPanel(sequenceNumbers: [101, 102], onBulkRemove: selected => captured = selected);

        InvokePrivateMethod(cut.Instance, "SetSelected", 101L, true);
        InvokePrivateMethod(cut.Instance, "SetSelected", 102L, true);

        cut.SetParametersAndRender(parameters => parameters
            .Add(p => p.Messages, CreateMessages([102])));

        await cut.InvokeAsync(() => InvokePrivateTask(cut.Instance, "DoBulkRemove"));

        captured.Should().BeEquivalentTo([102L]);
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

    private static void InvokePrivateMethod(object instance, string methodName, params object[] args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(instance.GetType().FullName, methodName);

        _ = method.Invoke(instance, args);
    }

    private static Task InvokePrivateTask(object instance, string methodName, params object[] args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(instance.GetType().FullName, methodName);

        var result = method.Invoke(instance, args);
        return result as Task ?? Task.CompletedTask;
    }
}
