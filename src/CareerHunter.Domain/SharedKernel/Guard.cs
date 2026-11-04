using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CareerHunter.Domain.SharedKernel;

/// <summary>
/// Small guard helpers used to keep invariants close to the model without
/// pulling in third-party validation libraries.
/// </summary>
internal static partial class Guard
{
    public static string NotBlank(
        string? value,
        string field,
        int maxLength,
        int minLength = 1)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            throw new DomainValidationException(field, $"{field} is required.");
        }

        if (normalized.Length < minLength || normalized.Length > maxLength)
        {
            throw new DomainValidationException(
                field,
                $"{field} must contain between {minLength} and {maxLength} characters.");
        }

        return normalized;
    }

    public static string? Optional(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new DomainValidationException(field, $"{field} must contain at most {maxLength} characters.");
        }

        return normalized;
    }

    public static int InRange(int value, string field, int min, int max)
    {
        if (value < min || value > max)
        {
            throw new DomainValidationException(field, $"{field} must be between {min} and {max}.");
        }

        return value;
    }

    public static decimal NotNegative(decimal value, string field)
    {
        if (value < 0)
        {
            throw new DomainValidationException(field, $"{field} cannot be negative.");
        }

        return value;
    }

    public static string AbsoluteHttpUrl(string? value, string field)
    {
        var normalized = NotBlank(value, field, 2048);
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new DomainValidationException(field, $"{field} must be an absolute http(s) URL.");
        }

        return uri.ToString();
    }

    public static DateTimeOffset UtcTimestamp(DateTimeOffset value, string field)
    {
        if (value == default)
        {
            throw new DomainValidationException(field, $"{field} is required.");
        }

        return value.ToUniversalTime();
    }

    public static string? Currency(string? value)
    {
        var normalized = Normalize(value);
        if (normalized.Length != 3 || !CurrencyPattern().IsMatch(normalized))
        {
            throw new DomainValidationException("currency", "Currency must be a 3-letter ISO code (for example BRL).");
        }

        return normalized;
    }

    private static string Normalize(string? value)
        => value?.Trim().ToUpperInvariant() ?? string.Empty;

    [GeneratedRegex("^[A-Z]{3}$", RegexOptions.None, 200)]
    private static partial Regex CurrencyPattern();
}

/// <summary>
/// Deterministic text normalization shared by deduplication, matching and search.
/// </summary>
public static class TextNormalizer
{
    private static readonly string[] CompanySuffixes =
    [
        "ltda", "me", "eireli", "sa", "s a", "inc", "llc", "corp", "corporation",
        "company", "group", "grupo", "tecnologia", "technology", "technologies", "sistemas", "systems"
    ];

    private static readonly string[] TitleNoise =
    [
        "vaga", "urgente", "remoto", "remota", "home office", "hibrido", "presencial",
        "pj", "clt", "100 remoto", "vagas"
    ];

    /// <summary>
    /// Lowercase, accent-insensitive, punctuation-free representation used for matching keys.
    /// </summary>
    public static string ForMatching(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(decomposed.Length);
        var previousWasSpace = false;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    public static string Company(string? name)
    {
        var normalized = ForMatching(name);
        foreach (var suffix in CompanySuffixes)
        {
            if (normalized.Length > suffix.Length && normalized.EndsWith(" " + suffix, StringComparison.Ordinal))
            {
                normalized = normalized[..^(suffix.Length + 1)].Trim();
            }
        }

        return normalized;
    }

    public static string JobTitle(string? title)
    {
        var normalized = ForMatching(title);
        foreach (var noise in TitleNoise)
        {
            normalized = normalized.Replace(noise, " ", StringComparison.Ordinal);
        }

        return CollapseSpaces(normalized);
    }

    public static string CollapseSpaces(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', parts);
    }

    /// <summary>
    /// Distinct tokens with at least two characters, used by the deterministic matcher.
    /// </summary>
    public static HashSet<string> Tokens(string? value)
        => ForMatching(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 2)
            .ToHashSet(StringComparer.Ordinal);
}
