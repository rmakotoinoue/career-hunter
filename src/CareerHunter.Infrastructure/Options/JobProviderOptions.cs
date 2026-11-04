namespace CareerHunter.Infrastructure.Options;

/// <summary>
/// Credentials and ingestion filters for the job boards that expose a public API.
/// Secrets live only in configuration (environment variables in production); the database
/// stores the search URL, never an API key.
/// </summary>
public sealed class JobProviderOptions
{
    public AdzunaOptions Adzuna { get; init; } = new();

    public JoobleOptions Jooble { get; init; } = new();

    public JobProviderFilterOptions Filter { get; init; } = new();
}

public sealed class AdzunaOptions
{
    public string? AppId { get; init; }

    public string? AppKey { get; init; }

    /// <summary>Adzuna accepts at most 50 results per page on the free plan.</summary>
    public int ResultsPerPage { get; init; } = 50;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(AppKey);
}

public sealed class JoobleOptions
{
    public string? ApiKey { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>
/// Client-side filters applied to boards that return every open position (Greenhouse, Lever).
/// An empty list disables that filter. Keywords are matched as case-insensitive substrings.
/// </summary>
public sealed class JobProviderFilterOptions
{
    public IReadOnlyList<string> TitleKeywords { get; init; } = [];

    public IReadOnlyList<string> LocationKeywords { get; init; } = [];
}
