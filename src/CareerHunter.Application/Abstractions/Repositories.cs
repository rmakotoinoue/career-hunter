using CareerHunter.Domain.Assessment;
using CareerHunter.Domain.Candidates;
using CareerHunter.Domain.Catalog;
using CareerHunter.Domain.Discovery;
using CareerHunter.Domain.Notifications;
using CareerHunter.Domain.Tracking;

namespace CareerHunter.Application.Abstractions;

public interface ICandidateProfileRepository
{
    Task<CandidateProfile?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(CandidateProfile profile);
}

public interface IJobSourceRepository
{
    Task<JobSource?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsWithNameAsync(
        string normalizedName,
        Guid? candidateProfileId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobSource>> ListAsync(
        bool onlyEnabled,
        Guid? candidateProfileId = null,
        CancellationToken cancellationToken = default);

    void Add(JobSource source);
}

public interface ISourceJobRecordRepository
{
    Task<bool> ExistsAsync(Guid jobSourceId, string contentHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns which of the supplied content hashes already exist for the source, in one query.
    /// Collection uses it instead of one round trip per feed item.
    /// </summary>
    Task<IReadOnlySet<string>> GetExistingHashesAsync(
        Guid jobSourceId,
        IReadOnlyCollection<string> contentHashes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns which of the supplied external ids already exist for the source, in one query.
    /// A feed item whose content changed keeps the same external id but produces a different hash,
    /// so identity has to be checked separately from the content hash.
    /// </summary>
    Task<IReadOnlySet<string>> GetExistingExternalIdsAsync(
        Guid jobSourceId,
        IReadOnlyCollection<string> externalIds,
        CancellationToken cancellationToken = default);

    Task<SourceJobRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SourceJobRecord>> ListPendingAsync(int take, CancellationToken cancellationToken = default);

    void Add(SourceJobRecord record);
}

public interface ICollectionRunRepository
{
    Task<CollectionRun?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(CollectionRun run);
}

public interface IEmployerRepository
{
    Task<Employer?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Employer?> GetByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default);

    void Add(Employer employer);
}

public interface IJobOpportunityRepository
{
    Task<JobOpportunity?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<JobOpportunity?> GetByDeduplicationKeyAsync(string deduplicationKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Used by the digest use case to snapshot title, employer and original URL.
    /// </summary>
    Task<IReadOnlyList<JobOpportunity>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    void Add(JobOpportunity opportunity);
}

public interface IJobEvaluationRepository
{
    Task<JobEvaluation?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<JobEvaluation?> GetLatestAsync(
        Guid candidateProfileId,
        Guid jobOpportunityId,
        CancellationToken cancellationToken = default);

    void Add(JobEvaluation evaluation);
}

public interface IEvaluationPromptVersionRepository
{
    Task<EvaluationPromptVersion?> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<EvaluationPromptVersion?> GetByVersionAsync(string version, CancellationToken cancellationToken = default);

    void Add(EvaluationPromptVersion promptVersion);
}

public interface IJobApplicationRepository
{
    Task<JobApplication?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<JobApplication?> GetByJobAsync(
        Guid candidateProfileId,
        Guid jobOpportunityId,
        CancellationToken cancellationToken = default);

    void Add(JobApplication application);
}

public interface INotificationPreferenceRepository
{
    Task<NotificationPreference?> GetByCandidateAsync(
        Guid candidateProfileId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationPreference>> ListEnabledAsync(
        CancellationToken cancellationToken = default);

    void Add(NotificationPreference preference);
}

public interface IDailyDigestRepository
{
    Task<DailyDigest?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<DailyDigest?> GetForDateAsync(
        Guid candidateProfileId,
        DateOnly digestDate,
        CancellationToken cancellationToken = default);

    void Add(DailyDigest digest);
}
