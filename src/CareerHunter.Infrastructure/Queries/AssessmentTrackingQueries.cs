using CareerHunter.Application.Abstractions;
using CareerHunter.Application.Assessment;
using CareerHunter.Application.Tracking;
using CareerHunter.Domain.Assessment;
using CareerHunter.Domain.Tracking;
using CareerHunter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareerHunter.Infrastructure.Queries;

public sealed class JobEvaluationQueries(CareerHunterDbContext db) : IJobEvaluationQueries
{
    public async Task<JobEvaluationDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var evaluation = await db.JobEvaluations
            .AsNoTracking()
            .Include(item => item.Dimensions)
            .Include(item => item.RequirementMatches)
            .ThenInclude(match => match.Evidence)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return evaluation is null ? null : Map(evaluation);
    }

    public async Task<IReadOnlyList<JobEvaluationSummaryDto>> ListByJobAsync(
        Guid jobOpportunityId,
        CancellationToken cancellationToken = default)
        => await SummaryQuery()
            .Where(row => row.Job.Id == jobOpportunityId)
            .OrderByDescending(row => row.Evaluation.EvaluatedAt)
            .Take(50)
            .Select(row => new JobEvaluationSummaryDto(
                row.Evaluation.Id,
                row.Job.Id,
                row.Job.Title,
                row.EmployerName,
                row.Job.SourceReferences.Select(reference => reference.SourceUrl).FirstOrDefault() ?? string.Empty,
                row.Evaluation.Engine,
                row.Evaluation.OverallScore,
                row.Evaluation.Recommendation,
                row.Evaluation.EvaluatedAt))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<JobEvaluationSummaryDto>> ListForDigestAsync(
        Guid candidateProfileId,
        DateTimeOffset sinceUtc,
        int minimumScore,
        int take,
        CancellationToken cancellationToken = default)
        => await SummaryQuery()
            .Where(row =>
                row.Evaluation.CandidateProfileId == candidateProfileId
                && row.Evaluation.EvaluatedAt >= sinceUtc
                && row.Evaluation.OverallScore >= minimumScore)
            .OrderByDescending(row => row.Evaluation.OverallScore)
            .ThenByDescending(row => row.Evaluation.EvaluatedAt)
            .Take(Math.Clamp(take, 1, 50))
            .Select(row => new JobEvaluationSummaryDto(
                row.Evaluation.Id,
                row.Job.Id,
                row.Job.Title,
                row.EmployerName,
                row.Job.SourceReferences.Select(reference => reference.SourceUrl).FirstOrDefault() ?? string.Empty,
                row.Evaluation.Engine,
                row.Evaluation.OverallScore,
                row.Evaluation.Recommendation,
                row.Evaluation.EvaluatedAt))
            .ToListAsync(cancellationToken);

    private IQueryable<EvaluationRow> SummaryQuery()
        => from evaluation in db.JobEvaluations.AsNoTracking()
           join job in db.JobOpportunities.AsNoTracking() on evaluation.JobOpportunityId equals job.Id
           join employer in db.Employers.AsNoTracking() on job.EmployerId equals employer.Id
           select new EvaluationRow
           {
               Evaluation = evaluation,
               Job = job,
               EmployerName = employer.Name
           };

    private static JobEvaluationDto Map(JobEvaluation evaluation)
        => new(
            evaluation.Id,
            evaluation.CandidateProfileId,
            evaluation.JobOpportunityId,
            evaluation.Engine,
            evaluation.OverallScore,
            evaluation.Recommendation,
            evaluation.EvaluatedAt,
            evaluation.ModelProvider,
            evaluation.ModelName,
            evaluation.PromptVersion,
            evaluation.InputFingerprint,
            evaluation.Dimensions
                .OrderBy(dimension => dimension.Dimension)
                .Select(dimension => new EvaluationDimensionDto(
                    dimension.Dimension,
                    dimension.Score,
                    dimension.Weight,
                    dimension.Rationale))
                .ToList(),
            evaluation.RequirementMatches
                .OrderByDescending(match => match.IsRequired)
                .ThenBy(match => match.RequirementName)
                .Select(match => new RequirementMatchDto(
                    match.RequirementName,
                    match.IsRequired,
                    match.Status,
                    match.Note,
                    match.Evidence
                        .Select(evidence => new EvaluationEvidenceDto(evidence.Kind, evidence.Text, evidence.Reference))
                        .ToList()))
                .ToList(),
            evaluation.Strengths,
            evaluation.Gaps,
            evaluation.Assumptions);

    private sealed class EvaluationRow
    {
        public required JobEvaluation Evaluation { get; init; }

        public required Domain.Catalog.JobOpportunity Job { get; init; }

        public required string EmployerName { get; init; }
    }
}

public sealed class JobApplicationQueries(CareerHunterDbContext db) : IApplicationQueries
{
    public async Task<ApplicationDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var application = await db.JobApplications
            .AsNoTracking()
            .Include(item => item.StatusHistory)
            .Include(item => item.Materials)
            .Include(item => item.Notes)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return application is null ? null : Map(application);
    }

    public async Task<IReadOnlyList<ApplicationSummaryDto>> ListAsync(
        Guid? candidateProfileId,
        ApplicationStatus? status,
        CancellationToken cancellationToken = default)
        => await db.JobApplications
            .AsNoTracking()
            .Where(application => candidateProfileId == null || application.CandidateProfileId == candidateProfileId)
            .Where(application => status == null || application.Status == status)
            .OrderByDescending(application => application.UpdatedAt)
            .Take(200)
            .Select(application => new ApplicationSummaryDto(
                application.Id,
                application.JobOpportunityId,
                application.Status,
                application.JobTitleSnapshot,
                application.EmployerNameSnapshot,
                application.JobUrlSnapshot,
                application.Materials.Count,
                application.AppliedAt,
                application.UpdatedAt))
            .ToListAsync(cancellationToken);

    private static ApplicationDto Map(JobApplication application)
        => new(
            application.Id,
            application.CandidateProfileId,
            application.JobOpportunityId,
            application.JobEvaluationId,
            application.Status,
            application.JobTitleSnapshot,
            application.EmployerNameSnapshot,
            application.JobUrlSnapshot,
            application.SalaryExpectation is null
                ? null
                : new MoneyDto(application.SalaryExpectation.Amount, application.SalaryExpectation.Currency),
            application.AppliedAt,
            application.CreatedAt,
            application.UpdatedAt,
            application.StatusHistory
                .OrderBy(change => change.ChangedAt)
                .Select(change => new ApplicationStatusChangeDto(
                    change.FromStatus,
                    change.ToStatus,
                    change.ChangedBy,
                    change.Reason,
                    change.ChangedAt))
                .ToList(),
            application.Materials
                .OrderBy(material => material.CreatedAt)
                .Select(material => new PreparedMaterialDto(
                    material.Id,
                    material.Type,
                    material.Title,
                    material.Content,
                    material.PreparedBy,
                    material.ModelName,
                    material.Version,
                    material.CreatedAt))
                .ToList(),
            application.Notes
                .OrderBy(note => note.CreatedAt)
                .Select(note => new ApplicationNoteDto(note.Id, note.Text, note.Author, note.CreatedAt))
                .ToList());
}
