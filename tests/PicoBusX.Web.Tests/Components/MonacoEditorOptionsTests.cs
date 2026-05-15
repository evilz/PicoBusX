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

    [Fact]
    public void TryValidateJson_ValidJson_ReturnsTrueWithNullError()
    {
        var isValid = MonacoEditorOptions.TryValidateJson("{\"name\":\"PicoBusX\"}", out var error);

        isValid.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void TryValidateJson_InvalidJson_ReturnsFalseWithError()
    {
        var isValid = MonacoEditorOptions.TryValidateJson("{bad json", out var error);

        isValid.Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TryFormatJson_ValidJson_ReturnsIndentedJson()
    {
        var result = MonacoEditorOptions.TryFormatJson("{\"name\":\"PicoBusX\",\"version\":10}", out var formattedJson, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
        formattedJson.Should().Be("{\n  \"name\": \"PicoBusX\",\n  \"version\": 10\n}");
    }

    [Fact]
    public void TryFormatJson_InvalidJson_ReturnsFalseAndOriginalValue()
    {
        var source = "{bad json";

        var result = MonacoEditorOptions.TryFormatJson(source, out var formattedJson, out var error);

        result.Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
        formattedJson.Should().Be(source);
    }

    [Fact]
    public void TryMinifyJson_ValidJson_ReturnsMinifiedJson()
    {
        const string source = "{\n  \"name\": \"PicoBusX\",\n  \"version\": 10,\n  \"features\": [\"format\", \"minify\"]\n}";

        var result = MonacoEditorOptions.TryMinifyJson(source, out var minifiedJson, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
        minifiedJson.Should().Be("{\"name\":\"PicoBusX\",\"version\":10,\"features\":[\"format\",\"minify\"]}");
    }

    [Fact]
    public void TryMinifyJson_InvalidJson_ReturnsFalseAndOriginalValue()
    {
        var source = "{bad json";

        var result = MonacoEditorOptions.TryMinifyJson(source, out var minifiedJson, out var error);

        result.Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
        minifiedJson.Should().Be(source);
    }
}
