using CareerHunter.Application.Assessment;
using CareerHunter.Application.Candidates;
using CareerHunter.Application.Catalog;
using CareerHunter.Application.Common;
using CareerHunter.Application.Discovery;
using CareerHunter.Application.Notifications;
using CareerHunter.Application.Tracking;
using CareerHunter.Domain.Discovery;

namespace CareerHunter.Application.Abstractions;

/// <summary>
/// Read-side ports. Implemented in Infrastructure with projections so list endpoints never
/// load full aggregates or trigger N+1 queries.
/// </summary>
public interface ICandidateProfileQueries
{
    Task<CandidateProfileDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CandidateProfileSummaryDto>> ListAsync(CancellationToken cancellationToken = default);
}

public interface IJobOpportunityQueries
{
    Task<JobOpportunityDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<JobOpportunitySummaryDto>> SearchAsync(
        JobSearchFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Active opportunities that still have no evaluation for the candidate. Used by the
    /// background pipeline so it never re-runs an engine for unchanged inputs.
    /// </summary>
    Task<IReadOnlyList<JobOpportunitySummaryDto>> ListUnevaluatedAsync(
        Guid candidateProfileId,
        int take,
        CancellationToken cancellationToken = default);
}

public interface IJobEvaluationQueries
{
    Task<JobEvaluationDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobEvaluationSummaryDto>> ListByJobAsync(
        Guid jobOpportunityId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobEvaluationSummaryDto>> ListForDigestAsync(
        Guid candidateProfileId,
        DateTimeOffset sinceUtc,
        int minimumScore,
        int take,
        CancellationToken cancellationToken = default);
}

public interface IApplicationQueries
{
    Task<ApplicationDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApplicationSummaryDto>> ListAsync(
        Guid? candidateProfileId,
        Domain.Tracking.ApplicationStatus? status,
        CancellationToken cancellationToken = default);
}

public interface IDiscoveryQueries
{
    Task<IReadOnlyList<JobSourceDto>> ListSourcesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CollectionRunDto>> ListRunsAsync(
        Guid? jobSourceId,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SourceJobRecordSummaryDto>> ListRecordsAsync(
        Guid? jobSourceId,
        SourceJobRecordStatus? status,
        int take,
        CancellationToken cancellationToken = default);
}

public interface IDailyDigestQueries
{
    Task<DailyDigestDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyDigestSummaryDto>> ListAsync(
        Guid? candidateProfileId,
        int take,
        CancellationToken cancellationToken = default);
}
