using System.Net;
using System.Text.RegularExpressions;

namespace CareerHunter.Infrastructure.Adapters;

/// <summary>
/// Normalizes text that arrives from job board APIs. Descriptions come as HTML (sometimes
/// HTML-escaped once more), so they are decoded and flattened before storage: the catalog keeps
/// human-readable text, and requirement extraction never sees markup.
/// </summary>
internal static partial class JobPostingText
{
    private const int MaximumDescriptionLength = 40_000;

    public static string ToPlainText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decoded = WebUtility.HtmlDecode(value);
        decoded = BreakTag().Replace(decoded, "\n");
        decoded = AnyTag().Replace(decoded, " ");
        decoded = WebUtility.HtmlDecode(decoded);
        decoded = HorizontalWhitespace().Replace(decoded, " ");
        decoded = RepeatedNewLines().Replace(decoded, "\n\n");

        var trimmed = decoded.Trim();
        return trimmed.Length <= MaximumDescriptionLength
            ? trimmed
            : trimmed[..MaximumDescriptionLength];
    }

    public static string? Optional(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    public static string Truncate(string? value, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    [GeneratedRegex(@"<\s*(br|/p|/li|/div|/h[1-6]|/tr)\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakTag();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.IgnoreCase)]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"[ \t\f\v]+")]
    private static partial Regex HorizontalWhitespace();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex RepeatedNewLines();
}
