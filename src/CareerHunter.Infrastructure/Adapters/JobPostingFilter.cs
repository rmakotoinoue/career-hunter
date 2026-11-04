using CareerHunter.Infrastructure.Options;

namespace CareerHunter.Infrastructure.Adapters;

/// <summary>
/// Trims the firehose of a company board down to the roles the candidate actually tracks.
/// Title keywords apply to every provider and can be replaced by the keywords derived from the
/// candidate profile; location keywords are opt-in per adapter because boards that already scope
/// the search by country would otherwise lose valid city-only locations (for example
/// "São Paulo, SP" has no country name).
/// </summary>
public sealed class JobPostingFilter(JobProviderFilterOptions options)
{
    public bool TitleMatches(string? title, IReadOnlyList<string>? candidateKeywords = null)
        => Matches(title, candidateKeywords is { Count: > 0 } ? candidateKeywords : options.TitleKeywords);

    public bool LocationMatches(string? location, IReadOnlyList<string>? candidateKeywords = null)
        => Matches(location, candidateKeywords is { Count: > 0 } ? candidateKeywords : options.LocationKeywords);

    private static bool Matches(string? value, IReadOnlyList<string> keywords)
    {
        if (keywords.Count == 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            // A configured filter cannot be satisfied by missing data; dropping the item keeps
            // the catalog honest instead of guessing the location.
            return false;
        }

        return keywords.Any(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
