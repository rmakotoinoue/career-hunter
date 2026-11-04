using CareerHunter.Application.Abstractions;
using CareerHunter.Application.Common;
using CareerHunter.Domain.Candidates;
using CareerHunter.Domain.Discovery;
using CareerHunter.Domain.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CareerHunter.Application.Discovery;

public sealed record RegisterJobSourceCommand(
    string Name,
    string BaseUrl,
    JobSourceKind Kind,
    string? AccessPolicyNotes,
    int MinimumIntervalMinutes = 360,
    Guid? CandidateProfileId = null);

public sealed class RegisterJobSourceUseCase(
    IJobSourceRepository repository,
    ICandidateProfileRepository candidates,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Guid> ExecuteAsync(RegisterJobSourceCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.CandidateProfileId is { } candidateId
            && !await candidates.ExistsAsync(candidateId, cancellationToken))
        {
            throw new NotFoundException("CandidateProfile", candidateId);
        }

        var normalizedName = TextNormalizer.ForMatching(command.Name);
        if (await repository.ExistsWithNameAsync(normalizedName, command.CandidateProfileId, cancellationToken))
        {
            throw new ConflictException($"A job source named '{command.Name}' already exists.");
        }

        var source = JobSource.Create(
            command.Name,
            command.BaseUrl,
            command.Kind,
            command.AccessPolicyNotes,
            command.MinimumIntervalMinutes,
            clock.UtcNow,
            command.CandidateProfileId);

        repository.Add(source);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return source.Id;
    }
}

public sealed record SetJobSourceEnabledCommand(Guid JobSourceId, bool Enabled);

/// <summary>
/// Pauses or resumes a source without deleting it. The worker skips disabled sources, which is
/// how a provider that still needs credentials stays registered but out of the daily cycle.
/// </summary>
public sealed class SetJobSourceEnabledUseCase(
    IJobSourceRepository repository,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task ExecuteAsync(
        SetJobSourceEnabledCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var source = await repository.GetAsync(command.JobSourceId, cancellationToken)
                     ?? throw new NotFoundException("JobSource", command.JobSourceId);

        source.SetEnabled(command.Enabled, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Collects raw records for one or every enabled source. Each source runs in its own unit of
/// work so a failure in one source never discards records collected from another.
/// Sources bound to a candidate are collected with the search plan derived from that profile,
/// so the same registered board can serve different candidates without hard-coded URLs.
/// </summary>
public sealed class CollectJobsUseCase(
    IJobSourceRepository jobSources,
    ISourceJobRecordRepository records,
    ICollectionRunRepository runs,
    ICandidateProfileRepository candidates,
    IEnumerable<IJobSourceAdapter> adapters,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<CollectJobsUseCase> logger)
{
    private const int MaxRecordsPerRun = 500;

    public async Task<IReadOnlyList<CollectionResultDto>> ExecuteAsync(
        Guid? jobSourceId,
        bool force = false,
        Guid? candidateProfileId = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<JobSource> sources;
        if (jobSourceId is null)
        {
            sources = await jobSources.ListAsync(onlyEnabled: true, candidateProfileId, cancellationToken);
        }
        else
        {
            var source = await jobSources.GetAsync(jobSourceId.Value, cancellationToken)
                         ?? throw new NotFoundException("JobSource", jobSourceId.Value);

            if (candidateProfileId is { } candidateId && source.CandidateProfileId != candidateId)
            {
                throw new NotFoundException("JobSource", jobSourceId.Value);
            }

            sources = [source];
        }

        var adapterMap = adapters
            .GroupBy(adapter => adapter.Kind)
            .ToDictionary(group => group.Key, group => group.First());

        var plans = await LoadSearchPlansAsync(sources, cancellationToken);

        var results = new List<CollectionResultDto>(sources.Count);
        var now = clock.UtcNow;

        foreach (var source in sources)
        {
            if (!source.IsEnabled || (!force && !source.CanCollect(now)))
            {
                continue;
            }

            var plan = source.CandidateProfileId is { } owner && plans.TryGetValue(owner, out var candidatePlan)
                ? candidatePlan
                : null;

            results.Add(await CollectFromSourceAsync(source, plan, adapterMap, now, cancellationToken));
        }

        return results;
    }

    /// <summary>
    /// Loads one plan per distinct candidate owner, never one per source, so a candidate with
    /// six company boards pays a single profile read per collection cycle.
    /// </summary>
    private async Task<Dictionary<Guid, JobSearchPlan>> LoadSearchPlansAsync(
        IReadOnlyList<JobSource> sources,
        CancellationToken cancellationToken)
    {
        var plans = new Dictionary<Guid, JobSearchPlan>();

        foreach (var candidateId in sources.Select(source => source.CandidateProfileId).OfType<Guid>().Distinct())
        {
            var profile = await candidates.GetAsync(candidateId, cancellationToken);
            if (profile is null)
            {
                throw new NotFoundException("CandidateProfile", candidateId);
            }

            var plan = JobSearchPlan.FromProfile(profile);
            if (!plan.IsEmpty)
            {
                plans[candidateId] = plan;
            }
        }

        return plans;
    }

    private async Task<CollectionResultDto> CollectFromSourceAsync(
        JobSource source,
        JobSearchPlan? searchPlan,
        IReadOnlyDictionary<JobSourceKind, IJobSourceAdapter> adapterMap,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var run = CollectionRun.Start(source.Id, now);
        runs.Add(run);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (!adapterMap.TryGetValue(source.Kind, out var adapter))
        {
            run.Fail($"No adapter is registered for source kind '{source.Kind}'.", clock.UtcNow);
            source.RegisterCollection(CollectionRunStatus.Failed, clock.UtcNow, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return ToResult(run, source);
        }

        IReadOnlyList<CollectedJob> collected;
        try
        {
            collected = await adapter.CollectAsync(source, searchPlan, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Job source {SourceId} failed during collection.", source.Id);
            run.Fail($"Source collection failed: {exception.Message}", clock.UtcNow);
            source.RegisterCollection(CollectionRunStatus.Failed, clock.UtcNow, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return ToResult(run, source);
        }

        var stored = 0;
        var skipped = 0;

        var batch = collected
            .Take(MaxRecordsPerRun)
            .Select(job => (Job: job, Hash: SourceJobRecord.ComputeContentHash(
                job.ExternalId,
                job.SourceUrl,
                job.Title,
                job.Company,
                job.Location,
                job.Description)))
            .ToArray();

        // One query for the whole batch instead of one round trip per feed item.
        var existingHashes = await records.GetExistingHashesAsync(
            source.Id,
            batch.Select(entry => entry.Hash).ToArray(),
            cancellationToken);

        // A feed item keeps its external id when the source edits its content, which changes the
        // hash. Checking both avoids a unique-constraint conflict on the second collection of the
        // same feed: the record is skipped instead of failing the whole run.
        var externalIds = batch
            .Select(entry => entry.Job.ExternalId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToArray();

        var existingExternalIds = await records.GetExistingExternalIdsAsync(
            source.Id,
            externalIds,
            cancellationToken);

        foreach (var (job, contentHash) in batch)
        {
            try
            {
                if (existingHashes.Contains(contentHash)
                    || (job.ExternalId is { Length: > 0 } externalId && existingExternalIds.Contains(externalId)))
                {
                    skipped++;
                    continue;
                }

                var record = SourceJobRecord.Create(
                    source.Id,
                    run.Id,
                    job.ExternalId,
                    job.SourceUrl,
                    job.Title,
                    job.Company,
                    job.Location,
                    job.Description,
                    job.PublishedAt,
                    clock.UtcNow,
                    clock.UtcNow);

                records.Add(record);
                stored++;
            }
            catch (DomainException exception)
            {
                run.RegisterRejection("record", exception.Message, clock.UtcNow);
            }
        }

        run.RegisterRecords(stored, skipped);
        run.Complete(clock.UtcNow);
        source.RegisterCollection(run.Status, clock.UtcNow, clock.UtcNow);

        // The (source, content_hash) unique index is the final duplicate guard: if a concurrent
        // collection inserted the same record between the query above and this save, the context
        // translates the violation into a conflict instead of a provider-specific error.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Collection run {RunId} for source {SourceId} finished with status {Status} ({Collected} stored, {Skipped} skipped).",
            run.Id,
            source.Id,
            run.Status,
            stored,
            skipped);

        return ToResult(run, source);
    }

    private static CollectionResultDto ToResult(CollectionRun run, JobSource source)
        => new(run.Id, source.Id, source.Name, run.Status, run.RecordsCollected, run.RecordsSkipped, run.RecordsRejected);
}
