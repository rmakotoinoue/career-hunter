using CareerHunter.Application.Abstractions;
using CareerHunter.Domain.Assessment;
using CareerHunter.Domain.Candidates;
using CareerHunter.Domain.Catalog;
using CareerHunter.Domain.Discovery;
using CareerHunter.Domain.Notifications;
using CareerHunter.Domain.Tracking;
using CareerHunter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareerHunter.Infrastructure.Repositories;

public sealed class CandidateProfileRepository(CareerHunterDbContext db) : ICandidateProfileRepository
{
    public Task<CandidateProfile?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => db.CandidateProfiles
            .Include(profile => profile.Experiences)
            .Include(profile => profile.Skills)
            .Include(profile => profile.Educations)
            .Include(profile => profile.Certifications)
            .Include(profile => profile.CareerTargets)
            .FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => db.CandidateProfiles.AnyAsync(profile => profile.Id == id, cancellationToken);

    public void Add(CandidateProfile profile) => db.CandidateProfiles.Add(profile);
}

public sealed class JobSourceRepository(CareerHunterDbContext db) : IJobSourceRepository
{
    public Task<JobSource?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => db.JobSources.FirstOrDefaultAsync(source => source.Id == id, cancellationToken);

    public Task<bool> ExistsWithNameAsync(
        string normalizedName,
        Guid? candidateProfileId = null,
        CancellationToken cancellationToken = default)
        => db.JobSources.AnyAsync(
            source => source.NormalizedName == normalizedName
                      && source.CandidateProfileId == candidateProfileId,
            cancellationToken);

    public async Task<IReadOnlyList<JobSource>> ListAsync(
        bool onlyEnabled,
        Guid? candidateProfileId = null,
        CancellationToken cancellationToken = default)
        => await db.JobSources
            .Where(source => !onlyEnabled || source.IsEnabled)
            .Where(source => candidateProfileId == null || source.CandidateProfileId == candidateProfileId)
            .OrderBy(source => source.Name)
            .ToListAsync(cancellationToken);

    public void Add(JobSource source) => db.JobSources.Add(source);
}

public sealed class SourceJobRecordRepository(CareerHunterDbContext db) : ISourceJobRecordRepository
{
    public Task<bool> ExistsAsync(Guid jobSourceId, string contentHash, CancellationToken cancellationToken = default)
        => db.SourceJobRecords.AnyAsync(
            record => record.JobSourceId == jobSourceId && record.ContentHash == contentHash,
            cancellationToken);

    public Task<SourceJobRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => db.SourceJobRecords.FirstOrDefaultAsync(record => record.Id == id, cancellationToken);

    public async Task<IReadOnlySet<string>> GetExistingHashesAsync(
        Guid jobSourceId,
        IReadOnlyCollection<string> contentHashes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentHashes);

        if (contentHashes.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var hashes = await db.SourceJobRecords
            .AsNoTracking()
            .Where(record => record.JobSourceId == jobSourceId && contentHashes.Contains(record.ContentHash))
            .Select(record => record.ContentHash)
            .ToListAsync(cancellationToken);

        return new HashSet<string>(hashes, StringComparer.Ordinal);
    }

    public async Task<IReadOnlySet<string>> GetExistingExternalIdsAsync(
        Guid jobSourceId,
        IReadOnlyCollection<string> externalIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(externalIds);

        if (externalIds.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var ids = await db.SourceJobRecords
            .AsNoTracking()
            .Where(record => record.JobSourceId == jobSourceId
                             && record.ExternalId != null
                             && externalIds.Contains(record.ExternalId))
            .Select(record => record.ExternalId!)
            .ToListAsync(cancellationToken);

        return new HashSet<string>(ids, StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<SourceJobRecord>> ListPendingAsync(
        int take,
        CancellationToken cancellationToken = default)
        => await db.SourceJobRecords
            .Where(record => record.Status == SourceJobRecordStatus.Collected)
            .OrderBy(record => record.CollectedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public void Add(SourceJobRecord record) => db.SourceJobRecords.Add(record);
}

public sealed class CollectionRunRepository(CareerHunterDbContext db) : ICollectionRunRepository
{
    public Task<CollectionRun?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => db.CollectionRuns
            .Include(run => run.Errors)
            .FirstOrDefaultAsync(run => run.Id == id, cancellationToken);

    public void Add(CollectionRun run) => db.CollectionRuns.Add(run);
}

public sealed class EmployerRepository(CareerHunterDbContext db) : IEmployerRepository
{
    public Task<Employer?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Employers.FirstOrDefaultAsync(employer => employer.Id == id, cancellationToken);

    public Task<Employer?> GetByNormalizedNameAsync(
        string normalizedName,
        CancellationToken cancellationToken = default)
        => db.Employers.FirstOrDefaultAsync(employer => employer.NormalizedName == normalizedName, cancellationToken);

    public void Add(Employer employer) => db.Employers.Add(employer);
}

public sealed class JobOpportunityRepository(CareerHunterDbContext db) : IJobOpportunityRepository
{
    public Task<JobOpportunity?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => db.JobOpportunities
            .Include(job => job.Requirements)
            .Include(job => job.SourceReferences)
            .FirstOrDefaultAsync(job => job.Id == id, cancellationToken);

    public Task<JobOpportunity?> GetByDeduplicationKeyAsync(
        string deduplicationKey,
        CancellationToken cancellationToken = default)
        => db.JobOpportunities
            .Include(job => job.Requirements)
            .Include(job => job.SourceReferences)
            .FirstOrDefaultAsync(job => job.DeduplicationKey == deduplicationKey, cancellationToken);

    public async Task<IReadOnlyList<JobOpportunity>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
        => await db.JobOpportunities
            .Include(job => job.Requirements)
            .Include(job => job.SourceReferences)
            .Where(job => ids.Contains(job.Id))
            .ToListAsync(cancellationToken);

    public void Add(JobOpportunity opportunity) => db.JobOpportunities.Add(opportunity);
}

public sealed class JobEvaluationRepository(CareerHunterDbContext db) : IJobEvaluationRepository
{
    public Task<JobEvaluation?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => db.JobEvaluations
            .Include(evaluation => evaluation.Dimensions)
            .Include(evaluation => evaluation.RequirementMatches)
            .ThenInclude(match => match.Evidence)
            .FirstOrDefaultAsync(evaluation => evaluation.Id == id, cancellationToken);

    public Task<JobEvaluation?> GetLatestAsync(
        Guid candidateProfileId,
        Guid jobOpportunityId,
        CancellationToken cancellationToken = default)
        => db.JobEvaluations
            .AsNoTracking()
            .Where(evaluation =>
                evaluation.CandidateProfileId == candidateProfileId
                && evaluation.JobOpportunityId == jobOpportunityId)
            .OrderByDescending(evaluation => evaluation.EvaluatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(JobEvaluation evaluation) => db.JobEvaluations.Add(evaluation);
}

public sealed class EvaluationPromptVersionRepository(CareerHunterDbContext db) : IEvaluationPromptVersionRepository
{
    public Task<EvaluationPromptVersion?> GetActiveAsync(CancellationToken cancellationToken = default)
        => db.EvaluationPromptVersions.FirstOrDefaultAsync(prompt => prompt.IsActive, cancellationToken);

    public Task<EvaluationPromptVersion?> GetByVersionAsync(
        string version,
        CancellationToken cancellationToken = default)
        => db.EvaluationPromptVersions.FirstOrDefaultAsync(prompt => prompt.Version == version, cancellationToken);

    public void Add(EvaluationPromptVersion promptVersion) => db.EvaluationPromptVersions.Add(promptVersion);
}

public sealed class JobApplicationRepository(CareerHunterDbContext db) : IJobApplicationRepository
{
    public Task<JobApplication?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => db.JobApplications
            .Include(application => application.StatusHistory)
            .Include(application => application.Materials)
            .Include(application => application.Notes)
            .FirstOrDefaultAsync(application => application.Id == id, cancellationToken);

    public Task<JobApplication?> GetByJobAsync(
        Guid candidateProfileId,
        Guid jobOpportunityId,
        CancellationToken cancellationToken = default)
        => db.JobApplications.FirstOrDefaultAsync(
            application => application.CandidateProfileId == candidateProfileId
                           && application.JobOpportunityId == jobOpportunityId,
            cancellationToken);

    public void Add(JobApplication application) => db.JobApplications.Add(application);
}

public sealed class NotificationPreferenceRepository(CareerHunterDbContext db) : INotificationPreferenceRepository
{
    public Task<NotificationPreference?> GetByCandidateAsync(
        Guid candidateProfileId,
        CancellationToken cancellationToken = default)
        => db.NotificationPreferences.FirstOrDefaultAsync(
            preference => preference.CandidateProfileId == candidateProfileId,
            cancellationToken);

    public async Task<IReadOnlyList<NotificationPreference>> ListEnabledAsync(
        CancellationToken cancellationToken = default)
        => await db.NotificationPreferences
            .Where(preference => preference.IsEnabled)
            .ToListAsync(cancellationToken);

    public void Add(NotificationPreference preference) => db.NotificationPreferences.Add(preference);
}

public sealed class DailyDigestRepository(CareerHunterDbContext db) : IDailyDigestRepository
{
    public Task<DailyDigest?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => db.DailyDigests
            .Include(digest => digest.Items)
            .Include(digest => digest.Deliveries)
            .FirstOrDefaultAsync(digest => digest.Id == id, cancellationToken);

    public Task<DailyDigest?> GetForDateAsync(
        Guid candidateProfileId,
        DateOnly digestDate,
        CancellationToken cancellationToken = default)
        => db.DailyDigests
            .Include(digest => digest.Items)
            .Include(digest => digest.Deliveries)
            .FirstOrDefaultAsync(
                digest => digest.CandidateProfileId == candidateProfileId && digest.DigestDate == digestDate,
                cancellationToken);

    public void Add(DailyDigest digest) => db.DailyDigests.Add(digest);
}
