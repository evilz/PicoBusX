using BlazorMonaco.Editor;

namespace PicoBusX.Web.Components;

/// <summary>
/// Shared factory methods for BlazorMonaco editor construction options.
/// </summary>
internal static class MonacoEditorOptions
{
    private static readonly System.Text.Json.JsonSerializerOptions IndentedJsonSerializerOptions = new()
    {
        WriteIndented = true
    };

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

    internal static bool TryValidateJson(string value, out string? error)
    {
        try
        {
            System.Text.Json.JsonDocument.Parse(value);
            error = null;
            return true;
        }
        catch (System.Text.Json.JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    internal static bool TryFormatJson(string value, out string formattedJson, out string? error)
    {
        if (!TryParseJson(value, out var jsonDocument, out error))
        {
            formattedJson = value;
            return false;
        }

        using (jsonDocument)
        {
            formattedJson = System.Text.Json.JsonSerializer.Serialize(jsonDocument.RootElement, IndentedJsonSerializerOptions);
            return true;
        }
    }

    internal static bool TryMinifyJson(string value, out string minifiedJson, out string? error)
    {
        if (!TryParseJson(value, out var jsonDocument, out error))
        {
            minifiedJson = value;
            return false;
        }

        using (jsonDocument)
        {
            minifiedJson = System.Text.Json.JsonSerializer.Serialize(jsonDocument.RootElement);
            return true;
        }
    }

    private static bool TryParseJson(string value, out System.Text.Json.JsonDocument jsonDocument, out string? error)
    {
        try
        {
            jsonDocument = System.Text.Json.JsonDocument.Parse(value);
            error = null;
            return true;
        }
        catch (System.Text.Json.JsonException ex)
        {
            jsonDocument = default!;
            error = ex.Message;
            return false;
        }
    }
}
