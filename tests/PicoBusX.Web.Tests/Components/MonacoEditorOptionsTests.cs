using FluentAssertions;
using PicoBusX.Web.Components;

namespace PicoBusX.Web.Tests.Components;

public class MonacoEditorOptionsTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsJson_EmptyOrWhitespace_ReturnsFalse(string? value)
    {
        MonacoEditorOptions.IsJson(value!).Should().BeFalse();
    }

    [Theory]
    [InlineData("{ \"key\": \"value\" }")]
    [InlineData("[1, 2, 3]")]
    public void IsJson_JsonObjectAndArray_ReturnsTrue(string value)
    {
        MonacoEditorOptions.IsJson(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("\"hello\"")]
    [InlineData("42")]
    [InlineData("3.14")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("null")]
    public void IsJson_JsonScalars_ReturnsTrue(string value)
    {
        MonacoEditorOptions.IsJson(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("plain text")]
    [InlineData("{ bad json")]
    [InlineData("(empty)")]
    [InlineData("{unclosed")]
    [InlineData("[1,2,")]
    public void IsJson_InvalidJson_ReturnsFalse(string value)
    {
        MonacoEditorOptions.IsJson(value).Should().BeFalse();
    }
}
