using FluentAssertions;
using PicoBusX.Web.Formatting;

namespace PicoBusX.Web.Tests.Formatting;

public class EntityDisplayFormatterTests
{
    [Fact]
    public void FormatTimeSpan_ReturnsNever_ForMaxValue()
    {
        EntityDisplayFormatter.FormatTimeSpan(TimeSpan.MaxValue).Should().Be("Never");
    }

    [Fact]
    public void FormatDate_ReturnsEmDash_ForNullValue()
    {
        EntityDisplayFormatter.FormatDate(null).Should().Be("—");
    }

    [Fact]
    public void FormatText_ReturnsEmDash_ForWhitespace()
    {
        EntityDisplayFormatter.FormatText("   ").Should().Be("—");
    }
}

