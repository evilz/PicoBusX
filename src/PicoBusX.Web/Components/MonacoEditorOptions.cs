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
}
