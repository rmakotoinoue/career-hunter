using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Domain.Catalog;

public enum JobRequirementCategory
{
    Technology = 1,
    Experience = 2,
    Education = 3,
    Language = 4,
    Certification = 5,
    Other = 6
}

/// <summary>
/// Normalized input for a requirement, produced by the normalization use case.
/// </summary>
public sealed record JobRequirementInput(
    string Name,
    JobRequirementCategory Category = JobRequirementCategory.Technology,
    bool IsRequired = true,
    string? RawText = null);

/// <summary>
/// Provenance of a normalized opportunity: which raw record and URL it came from.
/// </summary>
public sealed record JobSourceReferenceInput(
    Guid SourceJobRecordId,
    string JobSourceName,
    string SourceUrl,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CollectedAt);

public sealed class JobRequirement : Entity
{
    private JobRequirement()
    {
        Name = string.Empty;
        NormalizedName = string.Empty;
    }

    private JobRequirement(
        Guid jobOpportunityId,
        string name,
        JobRequirementCategory category,
        bool isRequired,
        string? rawText)
    {
        JobOpportunityId = jobOpportunityId;
        Name = Guard.NotBlank(name, "name", 200);
        NormalizedName = TextNormalizer.ForMatching(Name);
        Category = category;
        IsRequired = isRequired;
        RawText = Guard.Optional(rawText, "rawText", 2000);

        if (!Enum.IsDefined(category))
        {
            throw new DomainValidationException("category", "Unknown requirement category.");
        }
    }

    public Guid JobOpportunityId { get; private set; }

    public string Name { get; private set; }

    public string NormalizedName { get; private set; }

    public JobRequirementCategory Category { get; private set; }

    /// <summary>
    /// Required requirements weigh more, but a missing optional requirement never
    /// disqualifies an opportunity.
    /// </summary>
    public bool IsRequired { get; private set; }

    public string? RawText { get; private set; }

    internal static JobRequirement Create(
        Guid jobOpportunityId,
        string name,
        JobRequirementCategory category,
        bool isRequired,
        string? rawText)
        => new(jobOpportunityId, name, category, isRequired, rawText);

    internal void MarkRequired() => IsRequired = true;
}

public sealed class JobSourceReference : Entity
{
    private JobSourceReference()
    {
        JobSourceName = string.Empty;
        SourceUrl = string.Empty;
    }

    private JobSourceReference(
        Guid jobOpportunityId,
        Guid sourceJobRecordId,
        string jobSourceName,
        string sourceUrl,
        DateTimeOffset? publishedAt,
        DateTimeOffset collectedAt)
    {
        JobOpportunityId = jobOpportunityId;
        SourceJobRecordId = sourceJobRecordId != Guid.Empty
            ? sourceJobRecordId
            : throw new DomainValidationException("sourceJobRecordId", "Source record is required.");

        JobSourceName = Guard.NotBlank(jobSourceName, "jobSourceName", 120);
        SourceUrl = Guard.AbsoluteHttpUrl(sourceUrl, "sourceUrl");
        PublishedAt = publishedAt?.ToUniversalTime();
        CollectedAt = Guard.UtcTimestamp(collectedAt, "collectedAt");
    }

    public Guid JobOpportunityId { get; private set; }

    public Guid SourceJobRecordId { get; private set; }

    public string JobSourceName { get; private set; }

    public string SourceUrl { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset CollectedAt { get; private set; }

    internal static JobSourceReference Create(
        Guid jobOpportunityId,
        Guid sourceJobRecordId,
        string jobSourceName,
        string sourceUrl,
        DateTimeOffset? publishedAt,
        DateTimeOffset collectedAt)
        => new(jobOpportunityId, sourceJobRecordId, jobSourceName, sourceUrl, publishedAt, collectedAt);
}
