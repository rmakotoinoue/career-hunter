using CareerHunter.Application.Abstractions;
using CareerHunter.Application.Common;
using CareerHunter.Domain.Assessment;
using Microsoft.Extensions.Logging;

namespace CareerHunter.Application.Assessment;

public enum EvaluationEnginePreference
{
    Auto = 1,
    RuleBased = 2,
    Ai = 3
}

public sealed record EvaluateJobOpportunityCommand(
    Guid CandidateProfileId,
    Guid JobOpportunityId,
    EvaluationEnginePreference Preference = EvaluationEnginePreference.Auto,
    bool Force = false);

/// <summary>
/// Runs an evaluation and persists it. The AI path is optional; when it is unavailable or its
/// answer is invalid, the deterministic engine produces the result instead of failing the request.
/// </summary>
public sealed class EvaluateJobOpportunityUseCase(
    ICandidateProfileRepository candidates,
    IJobOpportunityRepository jobs,
    IEmployerRepository employers,
    IJobEvaluationRepository evaluations,
    IEvaluationPromptVersionRepository promptVersions,
    IAIProvider aiProvider,
    EvaluationSnapshotFactory snapshots,
    RuleBasedEvaluationEngine ruleBasedEngine,
    AiEvaluationEngine aiEngine,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<EvaluateJobOpportunityUseCase> logger)
{
    public async Task<Guid> ExecuteAsync(
        EvaluateJobOpportunityCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var profile = await candidates.GetAsync(command.CandidateProfileId, cancellationToken)
                      ?? throw new NotFoundException("CandidateProfile", command.CandidateProfileId);

        var job = await jobs.GetAsync(command.JobOpportunityId, cancellationToken)
                  ?? throw new NotFoundException("JobOpportunity", command.JobOpportunityId);

        if (profile.Skills.Count == 0 && profile.Experiences.Count == 0)
        {
            throw new BusinessRuleException(
                "The candidate profile has no skills or experiences. Register real data before evaluating opportunities.");
        }

        var employer = await employers.GetAsync(job.EmployerId, cancellationToken)
                       ?? throw new NotFoundException("Employer", job.EmployerId);

        var candidateSnapshot = snapshots.CreateCandidate(profile);
        var jobSnapshot = snapshots.CreateJob(job, employer.Name);
        var candidateJson = snapshots.ToJson(candidateSnapshot);
        var jobJson = snapshots.ToJson(jobSnapshot);

        // Reuse is decided before invoking the engine: a repeat request must not pay for an AI
        // call whose answer would be discarded. The post-engine check below still covers the
        // fallback path, where the engine that actually ran is only known afterwards.
        if (!command.Force
            && await FindReusableAsync(profile.Id, job.Id, candidateJson, jobJson, command.Preference, cancellationToken)
                is { } reusableId)
        {
            return reusableId;
        }

        IJobEvaluationEngine engine = command.Preference switch
        {
            EvaluationEnginePreference.RuleBased => ruleBasedEngine,
            EvaluationEnginePreference.Ai when !aiProvider.IsConfigured => throw new BusinessRuleException(
                "The AI provider is not configured. Use the rule-based engine or configure the provider."),
            EvaluationEnginePreference.Ai => aiEngine,
            _ => aiProvider.IsConfigured ? aiEngine : ruleBasedEngine
        };

        var draft = await engine.EvaluateAsync(candidateSnapshot, jobSnapshot, cancellationToken);

        var fingerprint = EvaluationSnapshotFactory.ComputeFingerprint(
            candidateJson,
            jobJson,
            draft.Engine,
            draft.PromptVersion);

        if (!command.Force)
        {
            var latest = await evaluations.GetLatestAsync(profile.Id, job.Id, cancellationToken);
            if (latest is not null && latest.InputFingerprint == fingerprint)
            {
                logger.LogInformation(
                    "Evaluation {EvaluationId} already covers the current inputs for job {JobId}; reusing it.",
                    latest.Id,
                    job.Id);
                return latest.Id;
            }
        }

        var evaluation = JobEvaluation.Create(
            profile.Id,
            job.Id,
            draft.Engine,
            draft.Dimensions,
            draft.RequirementMatches,
            draft.Strengths,
            draft.Gaps,
            draft.Assumptions,
            draft.ModelProvider,
            draft.ModelName,
            draft.PromptVersion,
            fingerprint,
            clock.UtcNow);

        evaluations.Add(evaluation);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Evaluation {EvaluationId} created for job {JobId} with score {Score} ({Engine}).",
            evaluation.Id,
            job.Id,
            evaluation.OverallScore,
            evaluation.Engine);

        return evaluation.Id;
    }

    /// <summary>
    /// Finds an existing evaluation whose fingerprint matches the inputs and the engine that the
    /// next evaluation would use. Returns null when the answer depends on a runtime fallback.
    /// </summary>
    private async Task<Guid?> FindReusableAsync(
        Guid candidateProfileId,
        Guid jobOpportunityId,
        string candidateJson,
        string jobJson,
        EvaluationEnginePreference preference,
        CancellationToken cancellationToken)
    {
        var expected = await ResolveExpectedEngineAsync(preference, cancellationToken);
        if (expected is null)
        {
            return null;
        }

        var (engine, promptVersion) = expected.Value;
        var fingerprint = EvaluationSnapshotFactory.ComputeFingerprint(
            candidateJson,
            jobJson,
            engine,
            promptVersion);

        var latest = await evaluations.GetLatestAsync(candidateProfileId, jobOpportunityId, cancellationToken);
        if (latest is null || latest.InputFingerprint != fingerprint)
        {
            return null;
        }

        logger.LogInformation(
            "Evaluation {EvaluationId} already covers the current inputs for job {JobId}; reusing it without calling the engine.",
            latest.Id,
            jobOpportunityId);

        return latest.Id;
    }

    private async Task<(EvaluationEngine Engine, string? PromptVersion)?> ResolveExpectedEngineAsync(
        EvaluationEnginePreference preference,
        CancellationToken cancellationToken)
    {
        switch (preference)
        {
            case EvaluationEnginePreference.RuleBased:
                return (EvaluationEngine.RuleBased, null);

            case EvaluationEnginePreference.Ai when !aiProvider.IsConfigured:
                // The engine selection below fails with an explicit business rule; no reuse here.
                return null;

            case EvaluationEnginePreference.Ai:
                break;

            case EvaluationEnginePreference.Auto when aiProvider.IsConfigured:
                break;

            default:
                // Auto without a configured provider always resolves to the deterministic engine.
                return (EvaluationEngine.RuleBased, null);
        }

        var promptVersion = await promptVersions.GetActiveAsync(cancellationToken);

        // Without an active prompt version the AI engine falls back to the rule engine, so the
        // engine that will run is only known after the call.
        return promptVersion is null ? null : (EvaluationEngine.Ai, promptVersion.Version);
    }
}
