using CareerHunter.Application.Catalog;
using CareerHunter.Domain.Assessment;
using CareerHunter.Domain.Notifications;
using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Application.Notifications;

public sealed record NotificationPreferenceDto(
    Guid Id,
    Guid CandidateProfileId,
    string Email,
    bool IsEnabled,
    int SendHourLocal,
    string TimeZoneId,
    int MinimumScore,
    DateTimeOffset UpdatedAt);

public sealed record DigestItemDto(
    Guid Id,
    Guid JobEvaluationId,
    Guid JobOpportunityId,
    int Rank,
    string JobTitle,
    string EmployerName,
    int Score,
    EvaluationRecommendation Recommendation,
    string JobUrl);

/// <summary>
/// One digest item joined with the stored job detail, so the dashboard renders a single
/// request instead of one call per item. Values that were never collected stay null;
/// they are never invented to fill the UI.
/// </summary>
public sealed record DigestItemDetailDto(
    Guid Id,
    Guid JobEvaluationId,
    Guid JobOpportunityId,
    int Rank,
    string JobTitle,
    string EmployerName,
    int Score,
    EvaluationRecommendation Recommendation,
    string JobUrl,
    string Location,
    WorkModality? Modality,
    EmploymentContractType? ContractType,
    SeniorityLevel? Seniority,
    JobCompensationDto? Compensation,
    string Description,
    DateTimeOffset? PublishedAt,
    string? SourceName,
    IReadOnlyList<JobRequirementDto> Requirements);

public sealed record NotificationDeliveryDto(
    Guid Id,
    string Provider,
    bool Succeeded,
    string? ErrorMessage,
    DateTimeOffset AttemptedAt);

public sealed record DailyDigestDto(
    Guid Id,
    Guid CandidateProfileId,
    DateOnly DigestDate,
    DigestStatus Status,
    DateTimeOffset GeneratedAt,
    DateTimeOffset? SentAt,
    string? LastError,
    int AttemptCount,
    IReadOnlyList<DigestItemDto> Items,
    IReadOnlyList<NotificationDeliveryDto> Deliveries);

public sealed record DailyDigestSummaryDto(
    Guid Id,
    DateOnly DigestDate,
    DigestStatus Status,
    int ItemCount,
    int AttemptCount,
    DateTimeOffset? SentAt);

public sealed record DigestGenerationResultDto(
    Guid DigestId,
    DateOnly DigestDate,
    DigestStatus Status,
    int ItemCount,
    bool AlreadyExisted);

public sealed record DigestDeliveryResultDto(
    Guid DigestId,
    DigestStatus Status,
    bool Delivered,
    int AttemptCount,
    string? Error);
