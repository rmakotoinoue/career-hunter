using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Domain.Assessment;

/// <summary>
/// Evaluation of a job opportunity against a candidate profile. AI output is advisory:
/// this aggregate recomputes the score from weighted dimensions and enforces the policy.
/// Evaluations are never updated or deleted, so historical comparison is always possible.
/// </summary>
public sealed class JobEvaluation : AggregateRoot
{
    private readonly List<EvaluationDimensionScore> _dimensions = [];
    private readonly List<RequirementMatch> _requirementMatches = [];
    private readonly List<string> _strengths = [];
    private readonly List<string> _gaps = [];
    private readonly List<string> _assumptions = [];

    private JobEvaluation()
    {
    }

    private JobEvaluation(
        Guid candidateProfileId,
        Guid jobOpportunityId,
        EvaluationEngine engine,
        IReadOnlyCollection<EvaluationDimensionInput> dimensions,
        IReadOnlyCollection<RequirementMatchInput> matches,
        IEnumerable<string> strengths,
        IEnumerable<string> gaps,
        IEnumerable<string> assumptions,
        string? modelProvider,
        string? modelName,
        string? promptVersion,
        string inputFingerprint,
        DateTimeOffset now)
        : base(Guid.NewGuid(), now)
    {
        CandidateProfileId = candidateProfileId != Guid.Empty
            ? candidateProfileId
            : throw new DomainValidationException("candidateProfileId", "Candidate profile is required.");

        JobOpportunityId = jobOpportunityId != Guid.Empty
            ? jobOpportunityId
            : throw new DomainValidationException("jobOpportunityId", "Job opportunity is required.");

        if (!Enum.IsDefined(engine))
        {
            throw new DomainValidationException("engine", "Unknown evaluation engine.");
        }

        Engine = engine;
        EvaluatedAt = Guard.UtcTimestamp(now, "evaluatedAt");
        InputFingerprint = Guard.NotBlank(inputFingerprint, "inputFingerprint", 128, 16);

        EvaluationScoringPolicy.ValidateDimensions(dimensions);
        EvaluationScoringPolicy.ValidateMatches(matches);

        var technicalScore = dimensions
            .Where(dimension => dimension.Dimension == EvaluationDimension.Technical)
            .Select(dimension => dimension.Score)
            .FirstOrDefault();

        EvaluationScoringPolicy.EnsureNotDisqualifiedBySecondaryGaps(technicalScore, matches);

        foreach (var dimension in dimensions)
        {
            _dimensions.Add(EvaluationDimensionScore.Create(Id, dimension.Dimension, dimension.Score, dimension.Weight, dimension.Rationale));
        }

        foreach (var match in matches)
        {
            _requirementMatches.Add(RequirementMatch.Create(
                Id,
                match.RequirementName,
                match.IsRequired,
                match.Status,
                match.Note,
                match.Evidence ?? []));
        }

        AddTexts(_strengths, strengths, "strengths");
        AddTexts(_gaps, gaps, "gaps");
        AddTexts(_assumptions, assumptions, "assumptions");

        OverallScore = EvaluationScoringPolicy.ComputeOverallScore(dimensions);
        Recommendation = EvaluationScoringPolicy.Recommend(OverallScore);
        ModelProvider = modelProvider is null ? null : Guard.NotBlank(modelProvider, "modelProvider", 80);
        ModelName = modelName is null ? null : Guard.NotBlank(modelName, "modelName", 120);
        PromptVersion = promptVersion is null ? null : Guard.NotBlank(promptVersion, "promptVersion", 40);

        if (engine == EvaluationEngine.Ai && (ModelProvider is null || ModelName is null || PromptVersion is null))
        {
            throw new DomainValidationException(
                "modelProvider",
                "AI evaluations must record provider, model and prompt version.");
        }
    }

    public Guid CandidateProfileId { get; private set; }

    public Guid JobOpportunityId { get; private set; }

    public EvaluationEngine Engine { get; private set; }

    /// <summary>
    /// Computed from weighted dimensions; never taken from the AI response directly.
    /// </summary>
    public int OverallScore { get; private set; }

    public EvaluationRecommendation Recommendation { get; private set; }

    public DateTimeOffset EvaluatedAt { get; private set; }

    public string? ModelProvider { get; private set; }

    public string? ModelName { get; private set; }

    public string? PromptVersion { get; private set; }

    /// <summary>
    /// Hash of candidate snapshot + job snapshot + engine + prompt version.
    /// Allows detecting whether a new evaluation would add information.
    /// </summary>
    public string InputFingerprint { get; private set; } = string.Empty;

    public IReadOnlyList<EvaluationDimensionScore> Dimensions => _dimensions;

    public IReadOnlyList<RequirementMatch> RequirementMatches => _requirementMatches;

    public IReadOnlyList<string> Strengths => _strengths;

    public IReadOnlyList<string> Gaps => _gaps;

    public IReadOnlyList<string> Assumptions => _assumptions;

    public static JobEvaluation Create(
        Guid candidateProfileId,
        Guid jobOpportunityId,
        EvaluationEngine engine,
        IReadOnlyCollection<EvaluationDimensionInput> dimensions,
        IReadOnlyCollection<RequirementMatchInput> matches,
        IEnumerable<string> strengths,
        IEnumerable<string> gaps,
        IEnumerable<string> assumptions,
        string? modelProvider,
        string? modelName,
        string? promptVersion,
        string inputFingerprint,
        DateTimeOffset now)
        => new(
            candidateProfileId,
            jobOpportunityId,
            engine,
            dimensions,
            matches,
            strengths,
            gaps,
            assumptions,
            modelProvider,
            modelName,
            promptVersion,
            inputFingerprint,
            now);

    private static void AddTexts(List<string> target, IEnumerable<string> values, string field)
    {
        foreach (var value in values)
        {
            var normalized = Guard.Optional(value, field, 1000);
            if (normalized is not null && !target.Contains(normalized, StringComparer.Ordinal))
            {
                target.Add(normalized);
            }
        }

        if (target.Count > 20)
        {
            throw new DomainValidationException(field, $"At most 20 entries are allowed for {field}.");
        }
    }
}

public sealed class EvaluationDimensionScore : Entity
{
    private EvaluationDimensionScore()
    {
        Rationale = string.Empty;
    }

    private EvaluationDimensionScore(
        Guid jobEvaluationId,
        EvaluationDimension dimension,
        int score,
        decimal weight,
        string rationale)
    {
        JobEvaluationId = jobEvaluationId;
        Dimension = dimension;
        Score = score;
        Weight = weight;
        Rationale = Guard.NotBlank(rationale, "rationale", 1000);
    }

    public Guid JobEvaluationId { get; private set; }

    public EvaluationDimension Dimension { get; private set; }

    public int Score { get; private set; }

    public decimal Weight { get; private set; }

    public string Rationale { get; private set; }

    internal static EvaluationDimensionScore Create(
        Guid jobEvaluationId,
        EvaluationDimension dimension,
        int score,
        decimal weight,
        string rationale)
        => new(jobEvaluationId, dimension, score, weight, rationale);
}

public sealed class RequirementMatch : Entity
{
    private readonly List<EvaluationEvidence> _evidence = [];

    private RequirementMatch()
    {
        RequirementName = string.Empty;
        NormalizedRequirementName = string.Empty;
    }

    private RequirementMatch(
        Guid jobEvaluationId,
        string requirementName,
        bool isRequired,
        RequirementMatchStatus status,
        string? note,
        IEnumerable<EvaluationEvidenceInput> evidence)
    {
        JobEvaluationId = jobEvaluationId;
        RequirementName = Guard.NotBlank(requirementName, "requirementName", 200);
        NormalizedRequirementName = TextNormalizer.ForMatching(RequirementName);
        IsRequired = isRequired;
        Status = status;
        Note = Guard.Optional(note, "note", 1000);

        foreach (var item in evidence)
        {
            _evidence.Add(EvaluationEvidence.Create(Id, item.Kind, item.Text, item.Reference));
        }

        if (_evidence.Count > 10)
        {
            throw new DomainValidationException("evidence", "At most 10 evidence items are allowed per requirement.");
        }
    }

    public Guid JobEvaluationId { get; private set; }

    public string RequirementName { get; private set; }

    public string NormalizedRequirementName { get; private set; }

    public bool IsRequired { get; private set; }

    public RequirementMatchStatus Status { get; private set; }

    public string? Note { get; private set; }

    public IReadOnlyList<EvaluationEvidence> Evidence => _evidence;

    internal static RequirementMatch Create(
        Guid jobEvaluationId,
        string requirementName,
        bool isRequired,
        RequirementMatchStatus status,
        string? note,
        IEnumerable<EvaluationEvidenceInput> evidence)
        => new(jobEvaluationId, requirementName, isRequired, status, note, evidence);
}

public sealed class EvaluationEvidence : Entity
{
    private EvaluationEvidence()
    {
        Text = string.Empty;
    }

    private EvaluationEvidence(Guid requirementMatchId, EvidenceKind kind, string text, string? reference)
    {
        RequirementMatchId = requirementMatchId;
        Kind = kind;
        Text = Guard.NotBlank(text, "text", 2000);
        Reference = Guard.Optional(reference, "reference", 200);
    }

    public Guid RequirementMatchId { get; private set; }

    public EvidenceKind Kind { get; private set; }

    public string Text { get; private set; }

    public string? Reference { get; private set; }

    internal static EvaluationEvidence Create(Guid requirementMatchId, EvidenceKind kind, string text, string? reference)
        => new(requirementMatchId, kind, text, reference);
}
