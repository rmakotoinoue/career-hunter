using CareerHunter.Domain.Assessment;
using CareerHunter.Domain.Notifications;

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
