using CareerHunter.Application.Abstractions;
using CareerHunter.Application.Discovery;
using CareerHunter.Application.Notifications;
using CareerHunter.Domain.Discovery;
using CareerHunter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareerHunter.Infrastructure.Queries;

public sealed class DiscoveryQueries(CareerHunterDbContext db) : IDiscoveryQueries
{
    public async Task<IReadOnlyList<JobSourceDto>> ListSourcesAsync(CancellationToken cancellationToken = default)
        => await db.JobSources
            .AsNoTracking()
            .OrderBy(source => source.Name)
            .Select(source => new JobSourceDto(
                source.Id,
                source.Name,
                source.BaseUrl,
                source.Kind,
                source.IsEnabled,
                source.MinimumIntervalMinutes,
                source.AccessPolicyNotes,
                source.LastCollectedAt,
                source.LastCollectionStatus,
                source.CandidateProfileId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CollectionRunDto>> ListRunsAsync(
        Guid? jobSourceId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var runs = await db.CollectionRuns
            .AsNoTracking()
            .Include(run => run.Errors)
            .Where(run => jobSourceId == null || run.JobSourceId == jobSourceId)
            .OrderByDescending(run => run.StartedAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);

        if (runs.Count == 0)
        {
            return [];
        }

        var sourceIds = runs.Select(run => run.JobSourceId).Distinct().ToArray();
        var names = await db.JobSources
            .AsNoTracking()
            .Where(source => sourceIds.Contains(source.Id))
            .ToDictionaryAsync(source => source.Id, source => source.Name, cancellationToken);

        return runs
            .Select(run => new CollectionRunDto(
                run.Id,
                run.JobSourceId,
                names.TryGetValue(run.JobSourceId, out var name) ? name : "unknown",
                run.StartedAt,
                run.CompletedAt,
                run.Status,
                run.RecordsCollected,
                run.RecordsSkipped,
                run.RecordsRejected,
                run.Errors
                    .OrderBy(error => error.OccurredAt)
                    .Select(error => new CollectionErrorDto(error.Stage, error.Message, error.OccurredAt))
                    .ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<SourceJobRecordSummaryDto>> ListRecordsAsync(
        Guid? jobSourceId,
        SourceJobRecordStatus? status,
        int take,
        CancellationToken cancellationToken = default)
        => await db.SourceJobRecords
            .AsNoTracking()
            .Where(record => jobSourceId == null || record.JobSourceId == jobSourceId)
            .Where(record => status == null || record.Status == status)
            .OrderByDescending(record => record.CollectedAt)
            .Take(Math.Clamp(take, 1, 200))
            .Select(record => new SourceJobRecordSummaryDto(
                record.Id,
                record.JobSourceId,
                record.SourceUrl,
                record.RawTitle,
                record.RawCompany,
                record.Status,
                record.PublishedAt,
                record.CollectedAt,
                record.NormalizedJobOpportunityId,
                record.RejectionReason))
            .ToListAsync(cancellationToken);
}

public sealed class DailyDigestQueries(CareerHunterDbContext db) : IDailyDigestQueries
{
    public async Task<DailyDigestDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var digest = await db.DailyDigests
            .AsNoTracking()
            .Include(item => item.Items)
            .Include(item => item.Deliveries)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (digest is null)
        {
            return null;
        }

        return new DailyDigestDto(
            digest.Id,
            digest.CandidateProfileId,
            digest.DigestDate,
            digest.Status,
            digest.GeneratedAt,
            digest.SentAt,
            digest.LastError,
            digest.Deliveries.Count,
            digest.Items
                .OrderBy(item => item.Rank)
                .Select(item => new DigestItemDto(
                    item.Id,
                    item.JobEvaluationId,
                    item.JobOpportunityId,
                    item.Rank,
                    item.JobTitle,
                    item.EmployerName,
                    item.Score,
                    item.Recommendation,
                    item.JobUrl))
                .ToList(),
            digest.Deliveries
                .OrderBy(delivery => delivery.AttemptedAt)
                .Select(delivery => new NotificationDeliveryDto(
                    delivery.Id,
                    delivery.Provider,
                    delivery.Succeeded,
                    delivery.ErrorMessage,
                    delivery.AttemptedAt))
                .ToList());
    }

    public async Task<IReadOnlyList<DailyDigestSummaryDto>> ListAsync(
        Guid? candidateProfileId,
        int take,
        CancellationToken cancellationToken = default)
        => await db.DailyDigests
            .AsNoTracking()
            .Where(digest => candidateProfileId == null || digest.CandidateProfileId == candidateProfileId)
            .OrderByDescending(digest => digest.DigestDate)
            .Take(Math.Clamp(take, 1, 100))
            .Select(digest => new DailyDigestSummaryDto(
                digest.Id,
                digest.DigestDate,
                digest.Status,
                digest.Items.Count,
                digest.Deliveries.Count,
                digest.SentAt))
            .ToListAsync(cancellationToken);
}
