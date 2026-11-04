using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Domain.Discovery;

/// <summary>
/// A single collection attempt for a source. Owns its errors; it never owns raw records
/// (those are separate aggregates so a run never loads thousands of descriptions).
/// </summary>
public sealed class CollectionRun : AggregateRoot
{
    private readonly List<CollectionError> _errors = [];

    private CollectionRun()
    {
        Status = CollectionRunStatus.Running;
    }

    private CollectionRun(Guid jobSourceId, DateTimeOffset startedAt)
        : base(Guid.NewGuid(), startedAt)
    {
        JobSourceId = jobSourceId != Guid.Empty
            ? jobSourceId
            : throw new DomainValidationException("jobSourceId", "Job source is required.");

        StartedAt = Guard.UtcTimestamp(startedAt, "startedAt");
        Status = CollectionRunStatus.Running;
    }

    public Guid JobSourceId { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public CollectionRunStatus Status { get; private set; }

    /// <summary>
    /// Records persisted by this run.
    /// </summary>
    public int RecordsCollected { get; private set; }

    /// <summary>
    /// Records ignored because an identical record already existed.
    /// </summary>
    public int RecordsSkipped { get; private set; }

    public int RecordsRejected { get; private set; }

    public IReadOnlyList<CollectionError> Errors => _errors;

    public static CollectionRun Start(Guid jobSourceId, DateTimeOffset startedAt)
        => new(jobSourceId, startedAt);

    public void RegisterRecords(int collected, int skipped)
    {
        if (Status != CollectionRunStatus.Running)
        {
            throw new DomainException("The collection run is already completed.");
        }

        RecordsCollected += Guard.InRange(collected, "collected", 0, 100_000);
        RecordsSkipped += Guard.InRange(skipped, "skipped", 0, 100_000);
    }

    public void RegisterRejection(string stage, string message, DateTimeOffset now)
    {
        if (Status != CollectionRunStatus.Running)
        {
            throw new DomainException("The collection run is already completed.");
        }

        _errors.Add(CollectionError.Create(Id, stage, message, now));
        RecordsRejected++;
        Touch(now);
    }

    public void Complete(DateTimeOffset now)
    {
        if (Status != CollectionRunStatus.Running)
        {
            throw new DomainException("The collection run is already completed.");
        }

        CompletedAt = Guard.UtcTimestamp(now, "completedAt");
        Status = _errors.Count == 0
            ? CollectionRunStatus.Succeeded
            : RecordsCollected > 0
                ? CollectionRunStatus.PartiallyFailed
                : CollectionRunStatus.Failed;

        Touch(now);
    }

    public void Fail(string reason, DateTimeOffset now)
    {
        if (Status != CollectionRunStatus.Running)
        {
            throw new DomainException("The collection run is already completed.");
        }

        _errors.Add(CollectionError.Create(Id, "collection", reason, now));
        CompletedAt = Guard.UtcTimestamp(now, "completedAt");
        Status = CollectionRunStatus.Failed;
        Touch(now);
    }
}

public sealed class CollectionError : Entity
{
    private CollectionError()
    {
        Stage = string.Empty;
        Message = string.Empty;
    }

    private CollectionError(Guid collectionRunId, string stage, string message, DateTimeOffset occurredAt)
    {
        CollectionRunId = collectionRunId;
        Stage = Guard.NotBlank(stage, "stage", 60);
        // Error messages come from external systems; keep the text but bound the size.
        Message = Guard.NotBlank(message, "message", 2000);
        OccurredAt = Guard.UtcTimestamp(occurredAt, "occurredAt");
    }

    public Guid CollectionRunId { get; private set; }

    public string Stage { get; private set; }

    public string Message { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    internal static CollectionError Create(Guid collectionRunId, string stage, string message, DateTimeOffset occurredAt)
    {
        var sanitized = message.Replace('\r', ' ').Replace('\n', ' ');
        return new CollectionError(collectionRunId, stage, sanitized, occurredAt);
    }
}
