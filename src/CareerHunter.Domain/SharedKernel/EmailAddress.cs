using System.Text.RegularExpressions;

namespace CareerHunter.Domain.SharedKernel;

/// <summary>
/// Validated e-mail address. Stored lower-case so equality and duplicate checks are stable.
/// </summary>
public sealed partial class EmailAddress : IEquatable<EmailAddress>
{
    private EmailAddress()
    {
        Value = string.Empty;
    }

    private EmailAddress(string value)
    {
        Value = value;
    }

    public string Value { get; private set; }

    public static EmailAddress Create(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized)
            || normalized.Length > 254
            || !EmailPattern().IsMatch(normalized))
        {
            throw new DomainValidationException("email", "A valid e-mail address is required.");
        }

        return new EmailAddress(normalized);
    }

    public bool Equals(EmailAddress? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as EmailAddress);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;

    [GeneratedRegex("^[^@\\s]{1,64}@[^@\\s]+\\.[^@\\s]{2,}$", RegexOptions.None, 200)]
    private static partial Regex EmailPattern();
}
