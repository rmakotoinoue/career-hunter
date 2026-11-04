using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Domain.Discovery;

public enum SourceJobRecordStatus
{
    Collected = 1,
    Normalized = 2,
    Rejected = 3
}

/// <summary>
/// Immutable raw content collected from a source. It is the audit trail of what was
/// actually published; normalization never overwrites it.
/// </summary>
public sealed class SourceJobRecord : AggregateRoot
{
    private SourceJobRecord()
    {
        SourceUrl = string.Empty;
        RawTitle = string.Empty;
        RawDescription = string.Empty;
        ContentHash = string.Empty;
    }

    private SourceJobRecord(
        Guid jobSourceId,
        Guid collectionRunId,
        string? externalId,
        string sourceUrl,
        string rawTitle,
        string? rawCompany,
        string? rawLocation,
        string rawDescription,
        DateTimeOffset? publishedAt,
        DateTimeOffset collectedAt,
        DateTimeOffset now)
        : base(Guid.NewGuid(), now)
    {
        JobSourceId = jobSourceId != Guid.Empty
            ? jobSourceId
            : throw new DomainValidationException("jobSourceId", "Job source is required.");

        CollectionRunId = collectionRunId != Guid.Empty
            ? collectionRunId
            : throw new DomainValidationException("collectionRunId", "Collection run is required.");

        ExternalId = Guard.Optional(externalId, "externalId", 200);
        SourceUrl = Guard.AbsoluteHttpUrl(sourceUrl, "sourceUrl");
        RawTitle = Guard.NotBlank(rawTitle, "rawTitle", 300);
        RawCompany = Guard.Optional(rawCompany, "rawCompany", 200);
        RawLocation = Guard.Optional(rawLocation, "rawLocation", 200);
        RawDescription = Guard.NotBlank(rawDescription, "rawDescription", 40_000, 20);
        PublishedAt = publishedAt?.ToUniversalTime();
        CollectedAt = Guard.UtcTimestamp(collectedAt, "collectedAt");

        if (PublishedAt is not null && PublishedAt > collectedAt)
        {
            throw new DomainValidationException("publishedAt", "Publication date cannot be after the collection date.");
        }

        ContentHash = ComputeContentHash(ExternalId, SourceUrl, RawTitle, RawCompany, RawLocation, RawDescription);
    }

    /// <summary>
    /// Same fingerprint used by the constructor and by the collection use case, so the
    /// duplicate check before persisting can never diverge from the stored value.
    /// </summary>
    public static string ComputeContentHash(
        string? externalId,
        string sourceUrl,
        string rawTitle,
        string? rawCompany,
        string? rawLocation,
        string rawDescription)
        => ContentFingerprint.Compute(externalId, sourceUrl, rawTitle, rawCompany, rawLocation, rawDescription);

    public Guid JobSourceId { get; private set; }

    public Guid CollectionRunId { get; private set; }

    public string? ExternalId { get; private set; }

    public string SourceUrl { get; private set; }

    public string RawTitle { get; private set; }

    public string? RawCompany { get; private set; }

    public string? RawLocation { get; private set; }

    public string RawDescription { get; private set; }

    /// <summary>
    /// Null when the source does not expose a publication date. Unknown is preserved as unknown.
    /// </summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset CollectedAt { get; private set; }

    public string ContentHash { get; private set; }

    public SourceJobRecordStatus Status { get; private set; } = SourceJobRecordStatus.Collected;

    public Guid? NormalizedJobOpportunityId { get; private set; }

    public string? RejectionReason { get; private set; }

    public static SourceJobRecord Create(
        Guid jobSourceId,
        Guid collectionRunId,
        string? externalId,
        string sourceUrl,
        string rawTitle,
        string? rawCompany,
        string? rawLocation,
        string rawDescription,
        DateTimeOffset? publishedAt,
        DateTimeOffset collectedAt,
        DateTimeOffset now)
        => new(
            jobSourceId,
            collectionRunId,
            externalId,
            sourceUrl,
            rawTitle,
            rawCompany,
            rawLocation,
            rawDescription,
            publishedAt,
            collectedAt,
            now);

    public void MarkNormalized(Guid jobOpportunityId, DateTimeOffset now)
    {
        if (jobOpportunityId == Guid.Empty)
        {
            throw new DomainValidationException("jobOpportunityId", "Job opportunity is required.");
        }

        if (Status == SourceJobRecordStatus.Rejected)
        {
            throw new DomainException("A rejected source record cannot be normalized.");
        }

        if (Status == SourceJobRecordStatus.Normalized && NormalizedJobOpportunityId != jobOpportunityId)
        {
            throw new DomainException("The source record was already normalized into another opportunity.");
        }

        Status = SourceJobRecordStatus.Normalized;
        NormalizedJobOpportunityId = jobOpportunityId;
        RejectionReason = null;
        Touch(now);
    }

    public void MarkRejected(string reason, DateTimeOffset now)
    {
        if (Status == SourceJobRecordStatus.Normalized)
        {
            throw new DomainException("A normalized source record cannot be rejected.");
        }

        RejectionReason = Guard.NotBlank(reason, "reason", 500);
        Status = SourceJobRecordStatus.Rejected;
        Touch(now);
    }
}
