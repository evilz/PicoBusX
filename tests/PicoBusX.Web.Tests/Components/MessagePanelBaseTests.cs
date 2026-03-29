using FluentAssertions;
using PicoBusX.Web.Components;

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
        public void RunOnInitialized() => OnInitialized();
        public void RunOnParametersSet() => OnParametersSet();

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
}
