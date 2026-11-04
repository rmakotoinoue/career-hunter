using CareerHunter.Domain.Catalog;
using CareerHunter.Domain.Discovery;
using CareerHunter.Domain.Tracking;

namespace CareerHunter.Application.Tracking;

public sealed record MoneyDto(decimal Amount, string Currency);

public sealed record ApplicationStatusChangeDto(
    ApplicationStatus? FromStatus,
    ApplicationStatus ToStatus,
    string ChangedBy,
    string? Reason,
    DateTimeOffset ChangedAt);

public sealed record PreparedMaterialDto(
    Guid Id,
    ApplicationMaterialType Type,
    string Title,
    string Content,
    string PreparedBy,
    string? ModelName,
    int Version,
    DateTimeOffset CreatedAt);

public sealed record ApplicationNoteDto(Guid Id, string Text, string Author, DateTimeOffset CreatedAt);

public sealed record ApplicationDto(
    Guid Id,
    Guid CandidateProfileId,
    Guid JobOpportunityId,
    Guid? JobEvaluationId,
    ApplicationStatus Status,
    string JobTitle,
    string EmployerName,
    string JobUrl,
    MoneyDto? SalaryExpectation,
    DateTimeOffset? AppliedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ApplicationStatusChangeDto> StatusHistory,
    IReadOnlyList<PreparedMaterialDto> Materials,
    IReadOnlyList<ApplicationNoteDto> Notes);

public sealed record ApplicationSummaryDto(
    Guid Id,
    Guid JobOpportunityId,
    ApplicationStatus Status,
    string JobTitle,
    string EmployerName,
    string JobUrl,
    int MaterialCount,
    DateTimeOffset? AppliedAt,
    DateTimeOffset UpdatedAt);

public sealed record ApplicationTrackingSnapshot(
    Guid JobOpportunityId,
    string JobTitle,
    string EmployerName,
    string JobUrl,
    JobOpportunityStatus JobStatus);
