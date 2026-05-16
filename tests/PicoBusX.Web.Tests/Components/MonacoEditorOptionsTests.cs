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

    // --- FormatJson ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FormatJson_EmptyOrWhitespace_ReturnsNull(string? value)
    {
        MonacoEditorOptions.FormatJson(value!).Should().BeNull();
    }

    [Fact]
    public void FormatJson_ValidCompactJson_ReturnsPrettyPrinted()
    {
        var result = MonacoEditorOptions.FormatJson("{\"a\":1,\"b\":2}");
        result.Should().NotBeNull();
        result.Should().Contain("\n");
        result.Should().Contain("  ");
        result.Should().Contain("\"a\"");
        result.Should().Contain("\"b\"");
    }

    [Fact]
    public void FormatJson_AlreadyFormattedJson_ReturnsNormalizedPrettyPrint()
    {
        var input = "{\n  \"a\": 1\n}";
        var result = MonacoEditorOptions.FormatJson(input);
        result.Should().NotBeNull();
        result.Should().Contain("\"a\"");
    }

    [Theory]
    [InlineData("{ bad json")]
    [InlineData("{unclosed")]
    public void FormatJson_InvalidJson_ReturnsNull(string value)
    {
        MonacoEditorOptions.FormatJson(value).Should().BeNull();
    }

    // --- MinifyJson ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MinifyJson_EmptyOrWhitespace_ReturnsNull(string? value)
    {
        MonacoEditorOptions.MinifyJson(value!).Should().BeNull();
    }

    [Fact]
    public void MinifyJson_PrettyPrintedJson_ReturnsCompact()
    {
        var input = "{\n  \"a\": 1,\n  \"b\": 2\n}";
        var result = MonacoEditorOptions.MinifyJson(input);
        result.Should().NotBeNull();
        result.Should().NotContain("\n");
        result.Should().NotContain("  ");
        result.Should().Contain("\"a\"");
        result.Should().Contain("\"b\"");
    }

    [Fact]
    public void MinifyJson_AlreadyCompactJson_ReturnsSameStructure()
    {
        var input = "{\"a\":1}";
        var result = MonacoEditorOptions.MinifyJson(input);
        result.Should().NotBeNull();
        result.Should().Contain("\"a\"");
        result.Should().NotContain("\n");
    }

    [Theory]
    [InlineData("{ bad json")]
    [InlineData("{unclosed")]
    public void MinifyJson_InvalidJson_ReturnsNull(string value)
    {
        MonacoEditorOptions.MinifyJson(value).Should().BeNull();
    }
}
