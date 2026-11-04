using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Domain.Catalog;

public enum JobOpportunityStatus
{
    Active = 1,
    Expired = 2
}

/// <summary>
/// Normalized job opportunity. It owns requirements and provenance, but never raw content
/// (which remains in the JobDiscovery context).
/// </summary>
public sealed class JobOpportunity : AggregateRoot
{
    private readonly List<JobRequirement> _requirements = [];
    private readonly List<JobSourceReference> _sourceReferences = [];

    private JobOpportunity()
    {
        Title = string.Empty;
        NormalizedTitle = string.Empty;
        Description = string.Empty;
        DeduplicationKey = string.Empty;
        Location = null!;
    }

    private JobOpportunity(
        Guid employerId,
        string employerNormalizedName,
        string title,
        JobLocation location,
        string description,
        DateTimeOffset now,
        WorkModality? modality,
        EmploymentContractType? contractType,
        SeniorityLevel? seniority,
        JobCompensation? compensation,
        DateTimeOffset? publishedAt)
        : base(Guid.NewGuid(), now)
    {
        EmployerId = employerId != Guid.Empty
            ? employerId
            : throw new DomainValidationException("employerId", "Employer is required.");

        Title = Guard.NotBlank(title, "title", 200);
        NormalizedTitle = TextNormalizer.JobTitle(Title);
        Location = location ?? throw new DomainValidationException("location", "Location is required.");
        Description = Guard.NotBlank(description, "description", 40_000, 20);
        ValidateEnum(modality, "modality");
        ValidateEnum(contractType, "contractType");
        ValidateEnum(seniority, "seniority");

        Modality = modality;
        ContractType = contractType;
        Seniority = seniority;
        Compensation = compensation;
        PublishedAt = publishedAt?.ToUniversalTime();

        DeduplicationKey = BuildDeduplicationKey(employerNormalizedName, Title, Location);
        Status = JobOpportunityStatus.Active;
        FirstSeenAt = now;
        LastSeenAt = now;
    }

    public Guid EmployerId { get; private set; }

    public string Title { get; private set; }

    public string NormalizedTitle { get; private set; }

    public JobLocation Location { get; private set; }

    /// <summary>
    /// Null when the source does not state the modality. Never guessed.
    /// </summary>
    public WorkModality? Modality { get; private set; }

    /// <summary>
    /// Null when the source does not state the contract type. Never guessed.
    /// </summary>
    public EmploymentContractType? ContractType { get; private set; }

    /// <summary>
    /// Null when the source does not state seniority. Never guessed.
    /// </summary>
    public SeniorityLevel? Seniority { get; private set; }

    public JobCompensation? Compensation { get; private set; }

    public string Description { get; private set; }

    /// <summary>
    /// Null when the source does not publish a date.
    /// </summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset FirstSeenAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public JobOpportunityStatus Status { get; private set; }

    public DateTimeOffset? ExpiredAt { get; private set; }

    /// <summary>
    /// Stable heuristic identity used to merge the same opening across sources:
    /// employer + normalized title + city/state. Unique at database level.
    /// </summary>
    public string DeduplicationKey { get; private set; }

    public IReadOnlyList<JobRequirement> Requirements => _requirements;

    public IReadOnlyList<JobSourceReference> SourceReferences => _sourceReferences;

    public bool IsActive => Status == JobOpportunityStatus.Active;

    public static JobOpportunity Create(
        Guid employerId,
        string employerNormalizedName,
        string title,
        JobLocation location,
        string description,
        DateTimeOffset now,
        WorkModality? modality = null,
        EmploymentContractType? contractType = null,
        SeniorityLevel? seniority = null,
        JobCompensation? compensation = null,
        DateTimeOffset? publishedAt = null)
        => new(
            employerId,
            employerNormalizedName,
            title,
            location,
            description,
            now,
            modality,
            contractType,
            seniority,
            compensation,
            publishedAt);

    public static string BuildDeduplicationKey(string? employerNormalizedName, string? title, JobLocation location)
    {
        var employer = string.IsNullOrWhiteSpace(employerNormalizedName)
            ? TextNormalizer.Company(employerNormalizedName)
            : employerNormalizedName.Trim().ToLowerInvariant();

        if (employer.Length == 0)
        {
            throw new DomainValidationException("employerNormalizedName", "Employer name is required for deduplication.");
        }

        var locationKey = TextNormalizer.ForMatching(
            $"{location?.City} {location?.State} {location?.Country}");

        var key = $"{employer}|{TextNormalizer.JobTitle(title)}|{locationKey}";
        if (key.Replace("|", string.Empty, StringComparison.Ordinal).Length == 0)
        {
            throw new DomainValidationException("deduplicationKey", "A deduplication key could not be built.");
        }

        return key;
    }

    public void UpdateNormalizedData(
        string title,
        JobLocation location,
        WorkModality? modality,
        EmploymentContractType? contractType,
        SeniorityLevel? seniority,
        JobCompensation? compensation,
        string description,
        DateTimeOffset? publishedAt,
        DateTimeOffset now)
    {
        Title = Guard.NotBlank(title, "title", 200);
        NormalizedTitle = TextNormalizer.JobTitle(Title);
        Location = location ?? throw new DomainValidationException("location", "Location is required.");
        Description = Guard.NotBlank(description, "description", 40_000, 20);
        ValidateEnum(modality, "modality");
        ValidateEnum(contractType, "contractType");
        ValidateEnum(seniority, "seniority");

        Modality = modality;
        ContractType = contractType;
        Seniority = seniority;
        Compensation = compensation;

        if (publishedAt is not null)
        {
            PublishedAt = publishedAt.Value.ToUniversalTime();
        }

        Touch(now);
    }

    public void ReplaceRequirements(IEnumerable<JobRequirementInput> requirements, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(requirements);

        _requirements.Clear();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var input in requirements)
        {
            var requirement = JobRequirement.Create(Id, input.Name, input.Category, input.IsRequired, input.RawText);
            if (!seen.Add(requirement.NormalizedName))
            {
                // Same requirement stated twice: keep the strongest statement.
                if (input.IsRequired)
                {
                    _requirements.First(existing => existing.NormalizedName == requirement.NormalizedName).MarkRequired();
                }

                continue;
            }

            _requirements.Add(requirement);
        }

        Touch(now);
    }

    /// <summary>
    /// Adds provenance. Returns false when the record was already linked, which keeps
    /// re-normalization idempotent.
    /// </summary>
    public bool AddSourceReference(JobSourceReferenceInput input, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (_sourceReferences.Any(reference => reference.SourceJobRecordId == input.SourceJobRecordId))
        {
            MarkSeen(now);
            return false;
        }

        var reference = JobSourceReference.Create(
            Id,
            input.SourceJobRecordId,
            input.JobSourceName,
            input.SourceUrl,
            input.PublishedAt,
            input.CollectedAt);

        _sourceReferences.Add(reference);

        if (PublishedAt is null || (reference.PublishedAt is not null && reference.PublishedAt < PublishedAt))
        {
            PublishedAt = reference.PublishedAt;
        }

        MarkSeen(now);
        return true;
    }

    public void MarkSeen(DateTimeOffset now)
    {
        LastSeenAt = Guard.UtcTimestamp(now, "lastSeenAt");
        if (Status == JobOpportunityStatus.Expired)
        {
            Status = JobOpportunityStatus.Active;
            ExpiredAt = null;
        }

        Touch(now);
    }

    public void Expire(DateTimeOffset now)
    {
        if (Status == JobOpportunityStatus.Expired)
        {
            return;
        }

        Status = JobOpportunityStatus.Expired;
        ExpiredAt = Guard.UtcTimestamp(now, "expiredAt");
        Touch(now);
    }

    private static void ValidateEnum<T>(T? value, string field) where T : struct, Enum
    {
        if (value is not null && !Enum.IsDefined(value.Value))
        {
            throw new DomainValidationException(field, $"Unknown {field}.");
        }
    }
}
