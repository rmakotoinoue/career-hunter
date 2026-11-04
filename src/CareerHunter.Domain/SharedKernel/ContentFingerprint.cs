using System.Security.Cryptography;
using System.Text;

namespace CareerHunter.Domain.SharedKernel;

/// <summary>
/// Deterministic content fingerprint used to avoid storing the same source record twice.
/// Based on accent/case-insensitive text so cosmetic changes in the source site do not
/// create endless duplicates, while real content changes do.
/// </summary>
public static class ContentFingerprint
{
    public static string Compute(params string?[] parts)
    {
        var canonical = string.Join(
            '\u001F',
            parts.Select(part => TextNormalizer.ForMatching(part)));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
