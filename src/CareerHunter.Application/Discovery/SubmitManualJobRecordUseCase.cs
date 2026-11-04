using CareerHunter.Application.Abstractions;
using CareerHunter.Application.Common;
using CareerHunter.Domain.Discovery;

namespace CareerHunter.Application.Discovery;

/// <summary>
/// Ingests one manually submitted opportunity. Provenance is kept identical to collected
/// records: a collection run is created so every normalized opportunity can be traced back
/// to where and when it was entered.
/// </summary>
public sealed record SubmitManualJobRecordCommand(
    Guid JobSourceId,
    string? ExternalId,
    string SourceUrl,
    string Title,
    string? Company,
    string? Location,
    string Description,
    DateTimeOffset? PublishedAt = null);

public sealed class SubmitManualJobRecordUseCase(
    IJobSourceRepository jobSources,
    ISourceJobRecordRepository records,
    ICollectionRunRepository runs,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Guid> ExecuteAsync(
        SubmitManualJobRecordCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var source = await jobSources.GetAsync(command.JobSourceId, cancellationToken)
                     ?? throw new NotFoundException("JobSource", command.JobSourceId);

        if (source.Kind != JobSourceKind.Manual)
        {
            throw new BusinessRuleException("Only a manual source accepts submitted records.");
        }

        var contentHash = SourceJobRecord.ComputeContentHash(
            command.ExternalId,
            command.SourceUrl,
            command.Title,
            command.Company,
            command.Location,
            command.Description);

        if (await records.ExistsAsync(source.Id, contentHash, cancellationToken))
        {
            throw new ConflictException("An identical record already exists for this source.");
        }

        var now = clock.UtcNow;
        var run = CollectionRun.Start(source.Id, now);
        runs.Add(run);

        var record = SourceJobRecord.Create(
            source.Id,
            run.Id,
            command.ExternalId,
            command.SourceUrl,
            command.Title,
            command.Company,
            command.Location,
            command.Description,
            command.PublishedAt,
            now,
            now);

        records.Add(record);

        run.RegisterRecords(collected: 1, skipped: 0);
        run.Complete(now);
        source.RegisterCollection(run.Status, now, now);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return record.Id;
    }
}
