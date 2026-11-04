using System.Net;
using System.Text.RegularExpressions;

namespace CareerHunter.Application.Common;

/// <summary>
/// Converts untrusted HTML/markup from job sources into bounded plain text.
/// Used before persisting normalized descriptions and before building AI prompts.
/// </summary>
public static partial class HtmlTextSanitizer
{
    public static string ToPlainText(string? html, int maxLength = 40_000)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var withoutScripts = ScriptOrStyle().Replace(html, " ");
        var withLineBreaks = BlockTags().Replace(withoutScripts, "\n");
        var withoutTags = Tags().Replace(withLineBreaks, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        var withoutControlCharacters = ControlCharacters().Replace(decoded, " ");

        var lines = withoutControlCharacters
            .Split('\n', StringSplitOptions.TrimEntries)
            .Select(Collapse)
            .Where(line => line.Length > 0);

        var text = string.Join('\n', lines);
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    /// <summary>
    /// Removes template placeholders and instruction-like delimiters so untrusted content
    /// cannot rewrite the prompt template that wraps it.
    /// </summary>
    public static string NeutralizePromptPayload(string text)
        => PlaceholderPattern().Replace(text, "[removed]");

    private static string Collapse(string value) => MultipleSpaces().Replace(value, " ").Trim();

    [GeneratedRegex("<(script|style)[^>]*>.*?</\\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline, 200)]
    private static partial Regex ScriptOrStyle();

    [GeneratedRegex("</?(p|br|div|li|ul|ol|h[1-6]|tr|section)[^>]*>", RegexOptions.IgnoreCase, 200)]
    private static partial Regex BlockTags();

    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline, 200)]
    private static partial Regex Tags();

    [GeneratedRegex("[\\u0000-\\u0008\\u000B\\u000C\\u000E-\\u001F\\u007F]", RegexOptions.None, 200)]
    private static partial Regex ControlCharacters();

    [GeneratedRegex("[ \\t]{2,}", RegexOptions.None, 200)]
    private static partial Regex MultipleSpaces();

    [GeneratedRegex("\\{\\{[^}]{0,80}\\}\\}", RegexOptions.None, 200)]
    private static partial Regex PlaceholderPattern();
}
