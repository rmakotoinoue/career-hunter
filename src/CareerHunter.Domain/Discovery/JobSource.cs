using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Domain.Discovery;

public enum JobSourceKind
{
    Manual = 1,
    Rss = 2,
    Api = 3,
    HtmlPage = 4,
    Adzuna = 5,
    Jooble = 6,
    Greenhouse = 7,
    Lever = 8
}

public enum CollectionRunStatus
{
    Running = 1,
    Succeeded = 2,
    PartiallyFailed = 3,
    Failed = 4
}

/// <summary>
/// A registered place where opportunities are discovered. The aggregate owns the
/// access policy and the collection cadence, never the raw content.
/// </summary>
public sealed class JobSource : AggregateRoot
{
    private JobSource()
    {
        Name = string.Empty;
        NormalizedName = string.Empty;
        BaseUrl = string.Empty;
    }

    private JobSource(
        string name,
        string baseUrl,
        JobSourceKind kind,
        Guid? candidateProfileId,
        string? accessPolicyNotes,
        int minimumIntervalMinutes,
        DateTimeOffset now)
        : base(Guid.NewGuid(), now)
    {
        Name = Guard.NotBlank(name, "name", 120);
        NormalizedName = TextNormalizer.ForMatching(Name);
        BaseUrl = Guard.AbsoluteHttpUrl(baseUrl, "baseUrl");
        Kind = kind;
        CandidateProfileId = candidateProfileId;
        AccessPolicyNotes = Guard.Optional(accessPolicyNotes, "accessPolicyNotes", 2000);
        MinimumIntervalMinutes = Guard.InRange(minimumIntervalMinutes, "minimumIntervalMinutes", 15, 1440);
        IsEnabled = true;

        if (!Enum.IsDefined(kind))
        {
            throw new DomainValidationException("kind", "Unknown job source kind.");
        }
    }

    public string Name { get; private set; }

    public string NormalizedName { get; private set; }

    public string BaseUrl { get; private set; }

    public JobSourceKind Kind { get; private set; }

    /// <summary>
    /// Candidate that owns this registration. When present, collection builds the search from
    /// that profile (career targets, skills, location) instead of the static query in the URL.
    /// Null means a shared source, still filtered by the global configuration keywords.
    /// </summary>
    public Guid? CandidateProfileId { get; private set; }

    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Free text documenting robots.txt, terms of use, authentication requirements and
    /// rate limits. Scraping protected platforms is explicitly out of policy.
    /// </summary>
    public string? AccessPolicyNotes { get; private set; }

    public int MinimumIntervalMinutes { get; private set; }

    public DateTimeOffset? LastCollectedAt { get; private set; }

    public CollectionRunStatus? LastCollectionStatus { get; private set; }

    public static JobSource Create(
        string name,
        string baseUrl,
        JobSourceKind kind,
        string? accessPolicyNotes,
        int minimumIntervalMinutes,
        DateTimeOffset now,
        Guid? candidateProfileId = null)
        => new(name, baseUrl, kind, candidateProfileId, accessPolicyNotes, minimumIntervalMinutes, now);

    /// <summary>
    /// Binds or unbinds the registration to a candidate. Null turns it back into a shared source
    /// governed by the global configuration keywords.
    /// </summary>
    public void AssignCandidate(Guid? candidateProfileId, DateTimeOffset now)
    {
        CandidateProfileId = candidateProfileId;
        Touch(now);
    }

    public void SetEnabled(bool enabled, DateTimeOffset now)
    {
        IsEnabled = enabled;
        Touch(now);
    }

    public void UpdateAccessPolicy(string? accessPolicyNotes, int minimumIntervalMinutes, DateTimeOffset now)
    {
        AccessPolicyNotes = Guard.Optional(accessPolicyNotes, "accessPolicyNotes", 2000);
        MinimumIntervalMinutes = Guard.InRange(minimumIntervalMinutes, "minimumIntervalMinutes", 15, 1440);
        Touch(now);
    }

    public void RegisterCollection(CollectionRunStatus status, DateTimeOffset collectedAt, DateTimeOffset now)
    {
        if (!Enum.IsDefined(status) || status == CollectionRunStatus.Running)
        {
            throw new DomainException("A completed collection status is required.");
        }

        LastCollectedAt = Guard.UtcTimestamp(collectedAt, "collectedAt");
        LastCollectionStatus = status;
        Touch(now);
    }

    public bool CanCollect(DateTimeOffset now)
        => IsEnabled
           && (LastCollectedAt is null
               || now - LastCollectedAt.Value >= TimeSpan.FromMinutes(MinimumIntervalMinutes));
}
