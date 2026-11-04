using CareerHunter.Application.Abstractions;
using CareerHunter.Application.Candidates;
using CareerHunter.Application.Catalog;
using CareerHunter.Application.Common;
using CareerHunter.Domain.Candidates;
using CareerHunter.Domain.Catalog;
using CareerHunter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareerHunter.Infrastructure.Queries;

public sealed class CandidateProfileQueries(CareerHunterDbContext db) : ICandidateProfileQueries
{
    public async Task<CandidateProfileDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var profile = await db.CandidateProfiles
            .AsNoTracking()
            .Include(candidate => candidate.Experiences)
            .Include(candidate => candidate.Skills)
            .Include(candidate => candidate.Educations)
            .Include(candidate => candidate.Certifications)
            .Include(candidate => candidate.CareerTargets)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return profile is null ? null : Map(profile);
    }

    public async Task<IReadOnlyList<CandidateProfileSummaryDto>> ListAsync(
        CancellationToken cancellationToken = default)
        => await db.CandidateProfiles
            .AsNoTracking()
            .OrderBy(candidate => candidate.FullName)
            .Select(candidate => new CandidateProfileSummaryDto(
                candidate.Id,
                candidate.FullName,
                candidate.Email.Value,
                candidate.Headline,
                candidate.CareerTargets
                    .Where(target => target.IsPrimary)
                    .Select(target => target.JobTitle)
                    .FirstOrDefault(),
                candidate.Skills.Count,
                candidate.Experiences.Count,
                candidate.UpdatedAt))
            .ToListAsync(cancellationToken);

    private static CandidateProfileDto Map(CandidateProfile profile)
        => new(
            profile.Id,
            profile.FullName,
            profile.Email.Value,
            profile.Headline,
            profile.Summary,
            profile.LinkedInUrl,
            profile.YearsOfExperience,
            new CandidateLocationDto(profile.Location.City, profile.Location.State, profile.Location.Country),
            profile.CompensationExpectation is null
                ? null
                : new SalaryRangeDto(
                    profile.CompensationExpectation.MinAmount,
                    profile.CompensationExpectation.MaxAmount,
                    profile.CompensationExpectation.Currency,
                    profile.CompensationExpectation.Period),
            profile.WorkPreferences is null
                ? null
                : new WorkPreferencesDto(
                    profile.WorkPreferences.AcceptsRemote,
                    profile.WorkPreferences.AcceptsHybrid,
                    profile.WorkPreferences.AcceptsOnsite,
                    profile.WorkPreferences.AcceptsClt,
                    profile.WorkPreferences.AcceptsPj),
            profile.Experiences
                .OrderByDescending(experience => experience.EndDate ?? DateOnly.MaxValue)
                .Select(experience => new CandidateExperienceDto(
                    experience.Id,
                    experience.Employer,
                    experience.Title,
                    experience.StartDate,
                    experience.EndDate,
                    experience.IsCurrent,
                    experience.Description,
                    experience.Technologies))
                .ToList(),
            profile.Skills
                .OrderByDescending(skill => skill.Proficiency)
                .ThenBy(skill => skill.Name)
                .Select(skill => new CandidateSkillDto(
                    skill.Id,
                    skill.Name,
                    skill.Category,
                    skill.Proficiency,
                    skill.YearsOfExperience))
                .ToList(),
            profile.Educations
                .Select(education => new EducationDto(
                    education.Id,
                    education.Institution,
                    education.Degree,
                    education.FieldOfStudy,
                    education.StartDate,
                    education.EndDate))
                .ToList(),
            profile.Certifications
                .Select(certification => new CertificationDto(
                    certification.Id,
                    certification.Name,
                    certification.Issuer,
                    certification.IssuedOn,
                    certification.ExpiresOn,
                    certification.CredentialUrl))
                .ToList(),
            profile.CareerTargets
                .Select(target => new CareerTargetDto(
                    target.Id,
                    target.JobTitle,
                    target.Seniority,
                    target.IsPrimary,
                    target.Notes))
                .ToList(),
            profile.CreatedAt,
            profile.UpdatedAt);
}

public sealed class JobOpportunityQueries(CareerHunterDbContext db) : IJobOpportunityQueries
{
    public async Task<JobOpportunityDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await db.JobOpportunities
            .AsNoTracking()
            .Include(opportunity => opportunity.Requirements)
            .Include(opportunity => opportunity.SourceReferences)
            .FirstOrDefaultAsync(opportunity => opportunity.Id == id, cancellationToken);

        if (job is null)
        {
            return null;
        }

        var employer = await db.Employers
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == job.EmployerId, cancellationToken);

        return Map(job, employer);
    }

    public async Task<PagedResult<JobOpportunitySummaryDto>> SearchAsync(
        JobSearchFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var query =
            from job in db.JobOpportunities.AsNoTracking()
            join employer in db.Employers.AsNoTracking() on job.EmployerId equals employer.Id
            select new { Job = job, EmployerName = employer.Name };

        if (filter.OnlyActive)
        {
            query = query.Where(row => row.Job.Status == JobOpportunityStatus.Active);
        }

        if (filter.Modality is not null)
        {
            query = query.Where(row => row.Job.Modality == filter.Modality);
        }

        if (filter.ContractType is not null)
        {
            query = query.Where(row => row.Job.ContractType == filter.ContractType);
        }

        if (filter.Seniority is not null)
        {
            query = query.Where(row => row.Job.Seniority == filter.Seniority);
        }

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var text = filter.Query.Trim();
            var normalized = Domain.SharedKernel.TextNormalizer.ForMatching(text);
            query = query.Where(row =>
                row.Job.NormalizedTitle.Contains(normalized)
                || row.EmployerName.ToLower().Contains(text.ToLower())
                || row.Job.Description.ToLower().Contains(text.ToLower()));
        }

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(row => row.Job.PublishedAt ?? row.Job.LastSeenAt)
            .ThenByDescending(row => row.Job.LastSeenAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new JobOpportunitySummaryDto(
                row.Job.Id,
                row.Job.Title,
                row.EmployerName,
                row.Job.Location.ToString(),
                row.Job.Modality,
                row.Job.ContractType,
                row.Job.Seniority,
                row.Job.Compensation?.MinAmount,
                row.Job.Compensation?.MaxAmount,
                row.Job.Compensation?.Currency,
                row.Job.Status,
                row.Job.PublishedAt,
                row.Job.LastSeenAt))
            .ToList();

        return new PagedResult<JobOpportunitySummaryDto>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<JobOpportunitySummaryDto>> ListUnevaluatedAsync(
        Guid candidateProfileId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(take, 1, 100);

        var rows = await (
                from job in db.JobOpportunities.AsNoTracking()
                join employer in db.Employers.AsNoTracking() on job.EmployerId equals employer.Id
                where job.Status == JobOpportunityStatus.Active
                      && !db.JobEvaluations.Any(evaluation =>
                          evaluation.CandidateProfileId == candidateProfileId
                          && evaluation.JobOpportunityId == job.Id)
                orderby job.PublishedAt ?? job.LastSeenAt descending, job.LastSeenAt descending
                select new { Job = job, EmployerName = employer.Name })
            .Take(limit)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new JobOpportunitySummaryDto(
                row.Job.Id,
                row.Job.Title,
                row.EmployerName,
                row.Job.Location.ToString(),
                row.Job.Modality,
                row.Job.ContractType,
                row.Job.Seniority,
                row.Job.Compensation?.MinAmount,
                row.Job.Compensation?.MaxAmount,
                row.Job.Compensation?.Currency,
                row.Job.Status,
                row.Job.PublishedAt,
                row.Job.LastSeenAt))
            .ToList();
    }

    private static JobOpportunityDto Map(JobOpportunity job, Employer? employer)
        => new(
            job.Id,
            job.Title,
            job.EmployerId,
            employer?.Name ?? "unknown",
            employer?.Website,
            job.Location.City ?? string.Empty,
            job.Location.State,
            job.Location.Country,
            job.Location.ToString(),
            job.Modality,
            job.ContractType,
            job.Seniority,
            job.Compensation is null
                ? null
                : new JobCompensationDto(
                    job.Compensation.MinAmount,
                    job.Compensation.MaxAmount,
                    job.Compensation.Currency,
                    job.Compensation.Period,
                    job.Compensation.IsEstimated,
                    job.Compensation.IsKnown),
            job.Description,
            job.PublishedAt,
            job.FirstSeenAt,
            job.LastSeenAt,
            job.Status,
            job.ExpiredAt,
            job.Requirements
                .OrderByDescending(requirement => requirement.IsRequired)
                .ThenBy(requirement => requirement.Name)
                .Select(requirement => new JobRequirementDto(requirement.Name, requirement.Category, requirement.IsRequired))
                .ToList(),
            job.SourceReferences
                .OrderByDescending(reference => reference.PublishedAt ?? reference.CollectedAt)
                .Select(reference => new JobSourceReferenceDto(
                    reference.SourceJobRecordId,
                    reference.JobSourceName,
                    reference.SourceUrl,
                    reference.PublishedAt,
                    reference.CollectedAt))
                .ToList());
}
