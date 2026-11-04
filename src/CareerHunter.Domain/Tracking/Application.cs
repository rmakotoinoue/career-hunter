using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Domain.Tracking;

public enum ApplicationStatus
{
    Discovered = 1,
    Analyzed = 2,
    ReadyForReview = 3,
    Prepared = 4,
    Applied = 5,
    Rejected = 6,
    Expired = 7
}

public enum ApplicationMaterialType
{
    Resume = 1,
    RecruiterMessage = 2,
    QuestionAnswer = 3,
    SalaryExpectation = 4,
    CoverLetter = 5
}

/// <summary>
/// Tracks the candidate's application process for one opportunity.
/// The system prepares materials; it never submits anything. The Applied transition
/// therefore requires explicit user confirmation.
/// </summary>
public sealed class JobApplication : AggregateRoot
{
    private static readonly Dictionary<ApplicationStatus, ApplicationStatus[]> AllowedTransitions = new()
    {
        [ApplicationStatus.Discovered] =
        [
            ApplicationStatus.Analyzed,
            ApplicationStatus.Expired
        ],
        [ApplicationStatus.Analyzed] =
        [
            ApplicationStatus.ReadyForReview,
            ApplicationStatus.Expired
        ],
        [ApplicationStatus.ReadyForReview] =
        [
            ApplicationStatus.Prepared,
            ApplicationStatus.Expired
        ],
        [ApplicationStatus.Prepared] =
        [
            ApplicationStatus.Applied,
            ApplicationStatus.Rejected,
            ApplicationStatus.Expired
        ],
        [ApplicationStatus.Applied] =
        [
            ApplicationStatus.Rejected
        ],
        [ApplicationStatus.Rejected] = [],
        [ApplicationStatus.Expired] = []
    };

    private readonly List<ApplicationStatusChange> _statusHistory = [];
    private readonly List<PreparedApplicationMaterial> _materials = [];
    private readonly List<ApplicationNote> _notes = [];

    private JobApplication()
    {
        JobTitleSnapshot = string.Empty;
        EmployerNameSnapshot = string.Empty;
        JobUrlSnapshot = string.Empty;
    }

    private JobApplication(
        Guid candidateProfileId,
        Guid jobOpportunityId,
        string jobTitleSnapshot,
        string employerNameSnapshot,
        string jobUrlSnapshot,
        Guid? jobEvaluationId,
        DateTimeOffset now)
        : base(Guid.NewGuid(), now)
    {
        CandidateProfileId = candidateProfileId != Guid.Empty
            ? candidateProfileId
            : throw new DomainValidationException("candidateProfileId", "Candidate profile is required.");

        JobOpportunityId = jobOpportunityId != Guid.Empty
            ? jobOpportunityId
            : throw new DomainValidationException("jobOpportunityId", "Job opportunity is required.");

        JobTitleSnapshot = Guard.NotBlank(jobTitleSnapshot, "jobTitle", 200);
        EmployerNameSnapshot = Guard.NotBlank(employerNameSnapshot, "employerName", 200);
        JobUrlSnapshot = Guard.AbsoluteHttpUrl(jobUrlSnapshot, "jobUrl");
        JobEvaluationId = jobEvaluationId;
        Status = ApplicationStatus.Discovered;

        _statusHistory.Add(ApplicationStatusChange.Create(Id, null, Status, "system", "Application tracking started.", now));
    }

    public Guid CandidateProfileId { get; private set; }

    public Guid JobOpportunityId { get; private set; }

    /// <summary>
    /// Evaluation that supported the application, when one exists.
    /// </summary>
    public Guid? JobEvaluationId { get; private set; }

    public ApplicationStatus Status { get; private set; }

    public string JobTitleSnapshot { get; private set; }

    public string EmployerNameSnapshot { get; private set; }

    /// <summary>
    /// Original job application URL. Never replaced by an internal link.
    /// </summary>
    public string JobUrlSnapshot { get; private set; }

    public DateTimeOffset? AppliedAt { get; private set; }

    /// <summary>
    /// Salary expectation used for this specific application (single value).
    /// </summary>
    public Money? SalaryExpectation { get; private set; }

    public IReadOnlyList<ApplicationStatusChange> StatusHistory => _statusHistory;

    public IReadOnlyList<PreparedApplicationMaterial> Materials => _materials;

    public IReadOnlyList<ApplicationNote> Notes => _notes;

    public bool IsTerminal => Status is ApplicationStatus.Rejected or ApplicationStatus.Expired;

    public static JobApplication Start(
        Guid candidateProfileId,
        Guid jobOpportunityId,
        string jobTitleSnapshot,
        string employerNameSnapshot,
        string jobUrlSnapshot,
        Guid? jobEvaluationId,
        DateTimeOffset now)
        => new(
            candidateProfileId,
            jobOpportunityId,
            jobTitleSnapshot,
            employerNameSnapshot,
            jobUrlSnapshot,
            jobEvaluationId,
            now);

    public static bool CanTransition(ApplicationStatus from, ApplicationStatus to)
        => AllowedTransitions.TryGetValue(from, out var targets) && targets.Contains(to);

    public void ChangeStatus(
        ApplicationStatus target,
        string changedBy,
        string? reason,
        bool userConfirmed,
        DateTimeOffset now)
    {
        if (!Enum.IsDefined(target))
        {
            throw new DomainValidationException("status", "Unknown application status.");
        }

        if (!CanTransition(Status, target))
        {
            throw new DomainException($"Transition from {Status} to {target} is not allowed.");
        }

        var author = Guard.NotBlank(changedBy, "changedBy", 120);

        switch (target)
        {
            case ApplicationStatus.ReadyForReview:
                RequireMaterial(ApplicationMaterialType.Resume, target);
                break;

            case ApplicationStatus.Prepared:
                RequireMaterial(ApplicationMaterialType.Resume, target);
                RequireMaterial(ApplicationMaterialType.RecruiterMessage, target);
                break;

            case ApplicationStatus.Applied:
                if (!userConfirmed)
                {
                    throw new DomainException("Marking an application as Applied requires explicit confirmation by the candidate.");
                }

                RequireMaterial(ApplicationMaterialType.Resume, target);
                if (SalaryExpectation is null)
                {
                    throw new DomainException("A salary expectation must be recorded before marking the application as Applied.");
                }

                AppliedAt = Guard.UtcTimestamp(now, "appliedAt");
                break;

            case ApplicationStatus.Rejected:
            case ApplicationStatus.Expired:
                if (string.IsNullOrWhiteSpace(reason))
                {
                    throw new DomainValidationException("reason", "A reason is required for this status.");
                }

                break;
        }

        var previous = Status;
        Status = target;
        _statusHistory.Add(ApplicationStatusChange.Create(Id, previous, target, author, reason, now));
        Touch(now);
    }

    public void SetSalaryExpectation(Money? expectation, DateTimeOffset now)
    {
        if (IsTerminal)
        {
            throw new DomainException("A closed application cannot be modified.");
        }

        SalaryExpectation = expectation;
        Touch(now);
    }

    public PreparedApplicationMaterial AddMaterial(
        ApplicationMaterialType type,
        string title,
        string content,
        string preparedBy,
        string? modelName,
        DateTimeOffset now)
    {
        if (IsTerminal)
        {
            throw new DomainException("A closed application cannot receive new materials.");
        }

        var version = _materials.Count(material => material.Type == type) + 1;
        var material = PreparedApplicationMaterial.Create(Id, type, title, content, preparedBy, modelName, version, now);
        _materials.Add(material);
        Touch(now);
        return material;
    }

    public ApplicationNote AddNote(string text, string author, DateTimeOffset now)
    {
        var note = ApplicationNote.Create(Id, text, author, now);
        _notes.Add(note);
        Touch(now);
        return note;
    }

    private void RequireMaterial(ApplicationMaterialType type, ApplicationStatus target)
    {
        if (!_materials.Any(material => material.Type == type))
        {
            throw new DomainException($"A {type} material is required before moving to {target}.");
        }
    }
}

public sealed class ApplicationStatusChange : Entity
{
    private ApplicationStatusChange()
    {
        ChangedBy = string.Empty;
    }

    private ApplicationStatusChange(
        Guid applicationId,
        ApplicationStatus? fromStatus,
        ApplicationStatus toStatus,
        string changedBy,
        string? reason,
        DateTimeOffset changedAt)
    {
        ApplicationId = applicationId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedBy = Guard.NotBlank(changedBy, "changedBy", 120);
        Reason = Guard.Optional(reason, "reason", 1000);
        ChangedAt = Guard.UtcTimestamp(changedAt, "changedAt");
    }

    public Guid ApplicationId { get; private set; }

    public ApplicationStatus? FromStatus { get; private set; }

    public ApplicationStatus ToStatus { get; private set; }

    public string ChangedBy { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    internal static ApplicationStatusChange Create(
        Guid applicationId,
        ApplicationStatus? fromStatus,
        ApplicationStatus toStatus,
        string changedBy,
        string? reason,
        DateTimeOffset changedAt)
        => new(applicationId, fromStatus, toStatus, changedBy, reason, changedAt);
}

public sealed class PreparedApplicationMaterial : Entity
{
    private PreparedApplicationMaterial()
    {
        Title = string.Empty;
        Content = string.Empty;
        PreparedBy = string.Empty;
    }

    private PreparedApplicationMaterial(
        Guid applicationId,
        ApplicationMaterialType type,
        string title,
        string content,
        string preparedBy,
        string? modelName,
        int version,
        DateTimeOffset now)
    {
        ApplicationId = applicationId;
        Type = type;
        Title = Guard.NotBlank(title, "title", 200);
        Content = Guard.NotBlank(content, "content", 20_000, 10);
        PreparedBy = Guard.NotBlank(preparedBy, "preparedBy", 80);
        ModelName = Guard.Optional(modelName, "modelName", 120);
        Version = Guard.InRange(version, "version", 1, 100);
        CreatedAt = Guard.UtcTimestamp(now, "createdAt");

        if (!Enum.IsDefined(type))
        {
            throw new DomainValidationException("type", "Unknown material type.");
        }
    }

    public Guid ApplicationId { get; private set; }

    public ApplicationMaterialType Type { get; private set; }

    public string Title { get; private set; }

    public string Content { get; private set; }

    /// <summary>
    /// Who produced the material: candidate, rule engine or an AI model.
    /// </summary>
    public string PreparedBy { get; private set; }

    public string? ModelName { get; private set; }

    public int Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static PreparedApplicationMaterial Create(
        Guid applicationId,
        ApplicationMaterialType type,
        string title,
        string content,
        string preparedBy,
        string? modelName,
        int version,
        DateTimeOffset now)
        => new(applicationId, type, title, content, preparedBy, modelName, version, now);
}

public sealed class ApplicationNote : Entity
{
    private ApplicationNote()
    {
        Text = string.Empty;
        Author = string.Empty;
    }

    private ApplicationNote(Guid applicationId, string text, string author, DateTimeOffset createdAt)
    {
        ApplicationId = applicationId;
        Text = Guard.NotBlank(text, "text", 4000);
        Author = Guard.NotBlank(author, "author", 120);
        CreatedAt = Guard.UtcTimestamp(createdAt, "createdAt");
    }

    public Guid ApplicationId { get; private set; }

    public string Text { get; private set; }

    public string Author { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static ApplicationNote Create(Guid applicationId, string text, string author, DateTimeOffset createdAt)
        => new(applicationId, text, author, createdAt);
}
