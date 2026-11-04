using System.Text.Json;
using CareerHunter.Application.Common;
using CareerHunter.Domain.Assessment;
using CareerHunter.Domain.Candidates;
using CareerHunter.Domain.Catalog;
using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Application.Assessment;

/// <summary>
/// Builds immutable, bounded snapshots of the candidate and the job. Snapshots are what the
/// engines see and what the evaluation fingerprint is computed from, so re-normalizing a job
/// later never rewrites the history of an evaluation.
/// </summary>
public sealed class EvaluationSnapshotFactory
{
    public const int MaxDescriptionLength = 12_000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CandidateProfileSnapshot CreateCandidate(CandidateProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var primaryTarget = profile.PrimaryCareerTarget;

        return new CandidateProfileSnapshot(
            profile.Id,
            RequiredText(profile.FullName, 160),
            RequiredText(profile.Headline, 200),
            OptionalText(profile.Summary, 4000),
            profile.YearsOfExperience,
            profile.Skills
                .Select(skill => new CandidateSkillSnapshot(
                    RequiredText(skill.Name, 120),
                    skill.Category.ToString(),
                    skill.Proficiency,
                    skill.YearsOfExperience))
                .ToArray(),
            profile.Experiences
                .Select(experience => new CandidateExperienceSnapshot(
                    RequiredText(experience.Employer, 200),
                    RequiredText(experience.Title, 200),
                    experience.StartDate,
                    experience.EndDate,
                    OptionalText(experience.Description, 2000),
                    experience.Technologies
                        .Select(technology => RequiredText(technology, 80))
                        .Where(technology => technology.Length > 0)
                        .ToArray()))
                .ToArray(),
            profile.Certifications
                .Select(certification => RequiredText($"{certification.Name} ({certification.Issuer})", 200))
                .ToArray(),
            primaryTarget is null ? null : RequiredText(primaryTarget.JobTitle, 200),
            primaryTarget?.Seniority.ToString(),
            profile.WorkPreferences?.Modalities().Select(modality => modality.ToString()).ToArray() ?? [],
            BuildContractList(profile.WorkPreferences),
            profile.CompensationExpectation?.MinAmount,
            profile.CompensationExpectation?.MaxAmount,
            profile.CompensationExpectation?.Currency,
            profile.CompensationExpectation?.Period.ToString());
    }

    public JobOpportunitySnapshot CreateJob(JobOpportunity job, string employerName)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentException.ThrowIfNullOrWhiteSpace(employerName);

        return new JobOpportunitySnapshot(
            job.Id,
            RequiredText(job.Title, 200),
            RequiredText(employerName, 200),
            RequiredText(job.Location.ToString(), 300),
            job.Modality?.ToString(),
            job.ContractType?.ToString(),
            job.Seniority?.ToString(),
            job.Compensation?.MinAmount,
            job.Compensation?.MaxAmount,
            job.Compensation?.Currency,
            job.Compensation?.Period.ToString(),
            RequiredText(job.Description, MaxDescriptionLength),
            job.PublishedAt,
            job.Requirements
                .Select(requirement => new JobRequirementSnapshot(
                    RequiredText(requirement.Name, 200),
                    requirement.Category.ToString(),
                    requirement.IsRequired))
                .ToArray());
    }

    public string ToJson<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    public static string ComputeFingerprint(
        string candidateJson,
        string jobJson,
        EvaluationEngine engine,
        string? promptVersion)
        => ContentFingerprint.Compute(candidateJson, jobJson, engine.ToString(), promptVersion);

    private static string RequiredText(string? value, int maxLength)
        => HtmlTextSanitizer.NeutralizePromptPayload(HtmlTextSanitizer.ToPlainText(value, maxLength));

    private static string? OptionalText(string? value, int maxLength)
    {
        var text = RequiredText(value, maxLength);
        return text.Length == 0 ? null : text;
    }

    private static IReadOnlyList<string> BuildContractList(WorkPreferences? preferences)
    {
        if (preferences is null)
        {
            return [];
        }

        var contracts = new List<string>(2);
        if (preferences.AcceptsClt)
        {
            contracts.Add(EmploymentContractType.Clt.ToString());
        }

        if (preferences.AcceptsPj)
        {
            contracts.Add(EmploymentContractType.Pj.ToString());
        }

        return contracts;
    }
}
