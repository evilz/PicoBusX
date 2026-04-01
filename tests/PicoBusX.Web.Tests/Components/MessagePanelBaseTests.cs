using FluentAssertions;
using Microsoft.AspNetCore.Components;
using PicoBusX.Web.Components;
using PicoBusX.Web.Models;

#pragma warning disable BL0005 // Setting [Parameter] properties directly is intentional in these unit tests

namespace PicoBusX.Web.Tests.Components;

public class MessagePanelBaseTests
{
    // Minimal concrete subclass that exposes protected members for testing
    private sealed class TestPanel : MessagePanelBase
    {
        public static string CallPrettyPrint(string body) => PrettyPrint(body);
        public void CallToggleMessage(long seq) => ToggleMessage(seq);
        public bool IsExpanded(long seq) => _expanded.Contains(seq);
        public int MaxCount => _maxCount;
        public string FilterText { get => _filterText; set => _filterText = value; }
        public IReadOnlyList<BrowsedMessage> CallFilterMessages(List<BrowsedMessage> messages) => FilterMessages(messages);
        public void RunOnInitialized() => OnInitialized();
        public void RunOnParametersSet() => OnParametersSet();
        public Task CallDoPeek() => DoPeek();
        public Task CallDoLoadMore() => DoLoadMore();

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) { }
    }

    // --- PrettyPrint ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PrettyPrint_EmptyOrWhitespace_ReturnsEmptyPlaceholder(string? body)
    {
        TestPanel.CallPrettyPrint(body!).Should().Be("(empty)");
    }

    [Fact]
    public void PrettyPrint_ValidJson_ReturnsIndented()
    {
        var result = TestPanel.CallPrettyPrint("{\"a\":1}");
        result.Should().Contain("\n");
        result.Should().Contain("\"a\"");
    }

    [Fact]
    public void PrettyPrint_InvalidJson_ReturnsOriginalBody()
    {
        var body = "not json at all";
        TestPanel.CallPrettyPrint(body).Should().Be(body);
    }

    // --- ToggleMessage ---

    [Fact]
    public void ToggleMessage_FirstCall_ExpandsMessage()
    {
        var panel = new TestPanel();
        panel.CallToggleMessage(42L);
        panel.IsExpanded(42L).Should().BeTrue();
    }

    [Fact]
    public void ToggleMessage_SecondCall_CollapsesMessage()
    {
        var panel = new TestPanel();
        panel.CallToggleMessage(42L);
        panel.CallToggleMessage(42L);
        panel.IsExpanded(42L).Should().BeFalse();
    }

    // --- OnInitialized / OnParametersSet ---

    [Theory]
    [InlineData(5, 5)]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    public void OnInitialized_SetsMaxCount(int defaultMaxCount, int expected)
    {
        var panel = new TestPanel { DefaultMaxCount = defaultMaxCount };
        panel.RunOnInitialized();
        panel.MaxCount.Should().Be(expected);
    }

    [Fact]
    public void OnParametersSet_WhenMaxCountIsZero_ResetsToDefault()
    {
        var panel = new TestPanel { DefaultMaxCount = 20 };
        // _maxCount starts at 0 (default int value)
        panel.RunOnParametersSet();
        panel.MaxCount.Should().Be(20);
    }

    [Fact]
    public void OnParametersSet_WhenMaxCountAlreadySet_LeavesItUnchanged()
    {
        var panel = new TestPanel { DefaultMaxCount = 5 };
        panel.RunOnInitialized();       // sets _maxCount = 5
        panel.DefaultMaxCount = 50;
        panel.RunOnParametersSet();     // _maxCount != 0, so no change
        panel.MaxCount.Should().Be(5);
    }

    // --- FilterMessages ---

    private static List<BrowsedMessage> SampleMessages() =>
    [
        new BrowsedMessage { MessageId = "msg-001", Subject = "OrderCreated", CorrelationId = "corr-1", SessionId = "sess-A", Body = "{\"order\":1}", SequenceNumber = 1 },
        new BrowsedMessage { MessageId = "msg-002", Subject = "ShipmentSent", CorrelationId = "corr-2", SessionId = "sess-B", Body = "plain text", SequenceNumber = 2, ApplicationProperties = new() { ["env"] = "prod" } },
        new BrowsedMessage { MessageId = "msg-003", Subject = null, CorrelationId = null, SessionId = null, Body = string.Empty, SequenceNumber = 3 },
    ];

    [Fact]
    public void FilterMessages_EmptyFilter_ReturnsAllMessages()
    {
        var panel = new TestPanel();
        var messages = SampleMessages();

        var result = panel.CallFilterMessages(messages);

        result.Should().HaveCount(3);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("")]
    public void FilterMessages_WhitespaceFilter_ReturnsAllMessages(string filter)
    {
        var panel = new TestPanel { FilterText = filter };
        var messages = SampleMessages();

        var result = panel.CallFilterMessages(messages);

        result.Should().HaveCount(3);
    }

    [Fact]
    public void FilterMessages_MatchOnMessageId_ReturnsMatchingMessage()
    {
        var panel = new TestPanel { FilterText = "msg-001" };
        var messages = SampleMessages();

        var result = panel.CallFilterMessages(messages);

        result.Should().ContainSingle(m => m.MessageId == "msg-001");
    }

    [Fact]
    public void FilterMessages_MatchOnSubject_ReturnsMatchingMessages()
    {
        var panel = new TestPanel { FilterText = "order" };
        var messages = SampleMessages();

        var result = panel.CallFilterMessages(messages);

        result.Should().ContainSingle(m => m.Subject == "OrderCreated");
    }

    [Fact]
    public void FilterMessages_MatchIsCaseInsensitive()
    {
        var panel = new TestPanel { FilterText = "ORDERCREATED" };
        var messages = SampleMessages();

        var result = panel.CallFilterMessages(messages);

        result.Should().ContainSingle(m => m.Subject == "OrderCreated");
    }

    [Fact]
    public void FilterMessages_MatchOnCorrelationId_ReturnsMatchingMessage()
    {
        var panel = new TestPanel { FilterText = "corr-2" };
        var messages = SampleMessages();

        var result = panel.CallFilterMessages(messages);

        result.Should().ContainSingle(m => m.MessageId == "msg-002");
    }

    [Fact]
    public void FilterMessages_MatchOnSessionId_ReturnsMatchingMessage()
    {
        var panel = new TestPanel { FilterText = "sess-A" };
        var messages = SampleMessages();

        var result = panel.CallFilterMessages(messages);

        result.Should().ContainSingle(m => m.MessageId == "msg-001");
    }

    [Fact]
    public void FilterMessages_MatchOnBody_ReturnsMatchingMessage()
    {
        var panel = new TestPanel { FilterText = "plain text" };
        var messages = SampleMessages();

        var result = panel.CallFilterMessages(messages);

        result.Should().ContainSingle(m => m.MessageId == "msg-002");
    }

    [Fact]
    public void FilterMessages_MatchOnApplicationPropertyValue_ReturnsMatchingMessage()
    {
        var panel = new TestPanel { FilterText = "prod" };
        var messages = SampleMessages();

        var result = panel.CallFilterMessages(messages);

        result.Should().ContainSingle(m => m.MessageId == "msg-002");
    }

    [Fact]
    public void FilterMessages_MatchOnApplicationPropertyKey_ReturnsMatchingMessage()
    {
        var panel = new TestPanel { FilterText = "env" };
        var messages = SampleMessages();

        var result = panel.CallFilterMessages(messages);

        result.Should().ContainSingle(m => m.MessageId == "msg-002");
    }

    [Fact]
    public void FilterMessages_NoMatch_ReturnsEmptyList()
    {
        var panel = new TestPanel { FilterText = "zzz-no-match" };
        var messages = SampleMessages();

        var result = panel.CallFilterMessages(messages);

        result.Should().BeEmpty();
    }

    // --- DoPeek ---

    [Fact]
    public async Task DoPeek_InvokesOnPeekWithNullSequence()
    {
        (string entityPath, int maxCount, long? fromSequenceNumber) captured = default;
        var panel = new TestPanel
        {
            EntityPath = "my-queue",
            OnPeek = EventCallback.Factory.Create<(string, int, long?)>(
                new object(), args => captured = args)
        };
        panel.RunOnInitialized();

        await panel.CallDoPeek();

        captured.entityPath.Should().Be("my-queue");
        captured.maxCount.Should().Be(10);
        captured.fromSequenceNumber.Should().BeNull();
    }

    // --- DoLoadMore ---

    [Fact]
    public async Task DoLoadMore_WithNoMessages_InvokesOnPeekWithNullFromSequence()
    {
        (string entityPath, int maxCount, long? fromSequenceNumber) captured = default;
        var panel = new TestPanel
        {
            EntityPath = "my-queue",
            Messages = new List<BrowsedMessage>(),
            OnPeek = EventCallback.Factory.Create<(string, int, long?)>(
                new object(), args => captured = args)
        };
        panel.RunOnInitialized();

        await panel.CallDoLoadMore();

        captured.fromSequenceNumber.Should().BeNull();
    }

    [Fact]
    public async Task DoLoadMore_WithMessages_InvokesOnPeekWithMaxSequencePlusOne()
    {
        (string entityPath, int maxCount, long? fromSequenceNumber) captured = default;
        var panel = new TestPanel
        {
            EntityPath = "my-queue",
            Messages =
            [
                new BrowsedMessage { SequenceNumber = 5 },
                new BrowsedMessage { SequenceNumber = 10 },
                new BrowsedMessage { SequenceNumber = 3 },
            ],
            OnPeek = EventCallback.Factory.Create<(string, int, long?)>(
                new object(), args => captured = args)
        };
        panel.RunOnInitialized();

        await panel.CallDoLoadMore();

        captured.fromSequenceNumber.Should().Be(11);
    }
}
