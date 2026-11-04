using CareerHunter.Domain.Catalog;
using CareerHunter.Domain.Discovery;

namespace CareerHunter.Application.Discovery;

public sealed record JobSourceDto(
    Guid Id,
    string Name,
    string BaseUrl,
    JobSourceKind Kind,
    bool IsEnabled,
    int MinimumIntervalMinutes,
    string? AccessPolicyNotes,
    DateTimeOffset? LastCollectedAt,
    CollectionRunStatus? LastCollectionStatus,
    Guid? CandidateProfileId = null);

public sealed record CollectionErrorDto(string Stage, string Message, DateTimeOffset OccurredAt);

public sealed record CollectionRunDto(
    Guid Id,
    Guid JobSourceId,
    string JobSourceName,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    CollectionRunStatus Status,
    int RecordsCollected,
    int RecordsSkipped,
    int RecordsRejected,
    IReadOnlyList<CollectionErrorDto> Errors);

public sealed record CollectionResultDto(
    Guid CollectionRunId,
    Guid JobSourceId,
    string JobSourceName,
    CollectionRunStatus Status,
    int RecordsCollected,
    int RecordsSkipped,
    int RecordsRejected);

public sealed record SourceJobRecordSummaryDto(
    Guid Id,
    Guid JobSourceId,
    string SourceUrl,
    string RawTitle,
    string? RawCompany,
    SourceJobRecordStatus Status,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CollectedAt,
    Guid? NormalizedJobOpportunityId,
    string? RejectionReason);

public sealed record NormalizationResultDto(
    int Processed,
    int Normalized,
    int Rejected,
    IReadOnlyList<string> RejectionReasons);
