using CareerHunter.Domain.Candidates;
using CareerHunter.Domain.Discovery;

namespace CareerHunter.Application.Abstractions;

/// <summary>
/// A source-specific collector. Implementations only know how to read a public feed/file;
/// they never bypass authentication, CAPTCHA or rate limits.
/// </summary>
public interface IJobSourceAdapter
{
    JobSourceKind Kind { get; }

    /// <summary>
    /// Collects the source. <paramref name="searchPlan"/> is the plan derived from the candidate
    /// that owns the source; when null or empty the adapter keeps the query/URL as registered.
    /// </summary>
    Task<IReadOnlyList<CollectedJob>> CollectAsync(
        JobSource source,
        JobSearchPlan? searchPlan = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Raw content as returned by a source adapter. Company may legitimately be unknown.
/// </summary>
public sealed record CollectedJob(
    string? ExternalId,
    string SourceUrl,
    string Title,
    string? Company,
    string? Location,
    string Description,
    DateTimeOffset? PublishedAt);
