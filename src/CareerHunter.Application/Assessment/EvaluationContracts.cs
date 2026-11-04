using CareerHunter.Domain.Assessment;

namespace CareerHunter.Application.Assessment;

public sealed record EvaluationDimensionDto(
    EvaluationDimension Dimension,
    int Score,
    decimal Weight,
    string Rationale);

public sealed record EvaluationEvidenceDto(EvidenceKind Kind, string Text, string? Reference);

public sealed record RequirementMatchDto(
    string RequirementName,
    bool IsRequired,
    RequirementMatchStatus Status,
    string? Note,
    IReadOnlyList<EvaluationEvidenceDto> Evidence);

public sealed record JobEvaluationDto(
    Guid Id,
    Guid CandidateProfileId,
    Guid JobOpportunityId,
    EvaluationEngine Engine,
    int OverallScore,
    EvaluationRecommendation Recommendation,
    DateTimeOffset EvaluatedAt,
    string? ModelProvider,
    string? ModelName,
    string? PromptVersion,
    string InputFingerprint,
    IReadOnlyList<EvaluationDimensionDto> Dimensions,
    IReadOnlyList<RequirementMatchDto> RequirementMatches,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Gaps,
    IReadOnlyList<string> Assumptions);

public sealed record JobEvaluationSummaryDto(
    Guid Id,
    Guid JobOpportunityId,
    string JobTitle,
    string EmployerName,
    string JobUrl,
    EvaluationEngine Engine,
    int OverallScore,
    EvaluationRecommendation Recommendation,
    DateTimeOffset EvaluatedAt);

/// <summary>
/// Structured request sent to the AI provider. The job description is untrusted text and is
/// carried separately from the instructions.
/// </summary>
public sealed record JobEvaluationRequest(
    string CandidateSnapshotJson,
    string JobSnapshotJson,
    string OutputSchemaJson,
    string PromptVersion);

/// <summary>
/// Validated result of an evaluation (rule engine or AI). The aggregate recomputes the
/// final score from these inputs.
/// </summary>
public sealed record JobEvaluationDraft(
    IReadOnlyCollection<EvaluationDimensionInput> Dimensions,
    IReadOnlyCollection<RequirementMatchInput> RequirementMatches,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Gaps,
    IReadOnlyList<string> Assumptions,
    EvaluationEngine Engine,
    string? ModelProvider,
    string? ModelName,
    string? PromptVersion);

public interface IJobEvaluationEngine
{
    Task<JobEvaluationDraft> EvaluateAsync(
        CandidateProfileSnapshot candidate,
        JobOpportunitySnapshot job,
        CancellationToken cancellationToken = default);
}

public sealed record CandidateSkillSnapshot(string Name, string Category, int Proficiency, decimal YearsOfExperience);

public sealed record CandidateExperienceSnapshot(
    string Employer,
    string Title,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Description,
    IReadOnlyList<string> Technologies);

public sealed record CandidateProfileSnapshot(
    Guid Id,
    string FullName,
    string Headline,
    string? Summary,
    int? YearsOfExperience,
    IReadOnlyList<CandidateSkillSnapshot> Skills,
    IReadOnlyList<CandidateExperienceSnapshot> Experiences,
    IReadOnlyList<string> Certifications,
    string? PrimaryTargetTitle,
    string? PrimaryTargetSeniority,
    IReadOnlyList<string> AcceptedModalities,
    IReadOnlyList<string> AcceptedContracts,
    decimal? ExpectedMinAmount,
    decimal? ExpectedMaxAmount,
    string? ExpectedCurrency,
    string? ExpectedPeriod);

public sealed record JobRequirementSnapshot(string Name, string Category, bool IsRequired);

public sealed record JobOpportunitySnapshot(
    Guid Id,
    string Title,
    string EmployerName,
    string Location,
    string? Modality,
    string? ContractType,
    string? Seniority,
    decimal? MinAmount,
    decimal? MaxAmount,
    string? Currency,
    string? SalaryPeriod,
    string Description,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<JobRequirementSnapshot> Requirements);
