using BlazorMonaco.Editor;

namespace PicoBusX.Web.Components;

/// <summary>
/// Shared factory methods for BlazorMonaco editor construction options.
/// </summary>
internal static class MonacoEditorOptions
{
    /// <summary>
    /// Returns read-only editor options with auto-detected language (JSON or plaintext).
    /// </summary>
    internal static StandaloneEditorConstructionOptions ReadOnly(StandaloneCodeEditor _, string value)
    {
        bool isJson = IsJson(value);
        return new StandaloneEditorConstructionOptions
        {
            AutomaticLayout = true,
            Language = isJson ? "json" : "plaintext",
            Value = value,
            ReadOnly = true,
            Theme = "vs-dark",
            Minimap = new EditorMinimapOptions { Enabled = false },
            ScrollBeyondLastLine = false,
            FontSize = 13,
            LineNumbers = "off",
            WordWrap = "on",
            RenderLineHighlight = "none"
        };
    }

    internal static bool IsJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            System.Text.Json.JsonDocument.Parse(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns a pretty-printed (indented) version of the JSON string, or null if the input is invalid JSON.
    /// </summary>
    internal static string? FormatJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(value);
            return System.Text.Json.JsonSerializer.Serialize(
                doc.RootElement,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Returns a minified (compact, no whitespace) version of the JSON string, or null if the input is invalid JSON.
    /// </summary>
    internal static string? MinifyJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(value);
            return System.Text.Json.JsonSerializer.Serialize(doc.RootElement);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
