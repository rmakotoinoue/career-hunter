using System.Globalization;
using System.Text.Json;

namespace CareerHunter.Infrastructure.Adapters;

/// <summary>
/// Small, allocation-friendly accessors for the heterogeneous JSON shapes returned by job
/// boards. Missing fields are returned as null so callers can reject an item instead of
/// inventing data.
/// </summary>
internal static class JsonFields
{
    public static string? Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static string? NestedText(JsonElement element, string name, string property)
        => element.TryGetProperty(name, out var nested) && nested.ValueKind == JsonValueKind.Object
            ? Text(nested, property)
            : null;

    /// <summary>Identifiers may be numbers (Greenhouse) or strings (Adzuna, Lever).</summary>
    public static string? Identifier(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    public static DateTimeOffset? Date(JsonElement element, string name)
    {
        var raw = Text(element, name);
        return ParseDate(raw);
    }

    public static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    public static DateTimeOffset? FromUnixMilliseconds(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var milliseconds))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        }

        return ParseDate(value.ValueKind == JsonValueKind.String ? value.GetString() : null);
    }

    public static Uri? AbsoluteHttpUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri
            : null;
}
