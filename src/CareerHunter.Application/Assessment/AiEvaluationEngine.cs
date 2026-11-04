using System.Text.Json;
using CareerHunter.Application.Abstractions;
using CareerHunter.Domain.Assessment;
using Microsoft.Extensions.Logging;

namespace CareerHunter.Application.Assessment;

/// <summary>
/// AI-backed engine. It never trusts the model: the answer is parsed strictly, validated,
/// and if anything fails the deterministic engine produces the evaluation instead.
/// </summary>
public sealed class AiEvaluationEngine(
    IAIProvider provider,
    IEvaluationPromptVersionRepository promptVersions,
    EvaluationSnapshotFactory snapshotFactory,
    EvaluationPromptBuilder promptBuilder,
    JobEvaluationResultValidator validator,
    RuleBasedEvaluationEngine fallback,
    ILogger<AiEvaluationEngine> logger) : IJobEvaluationEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        MaxDepth = 32
    };

    public async Task<JobEvaluationDraft> EvaluateAsync(
        CandidateProfileSnapshot candidate,
        JobOpportunitySnapshot job,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(job);

        var promptVersion = await promptVersions.GetActiveAsync(cancellationToken);
        if (promptVersion is null)
        {
            logger.LogWarning("No active evaluation prompt version found; falling back to the rule engine.");
            return await FallbackAsync(candidate, job, "no active prompt version", cancellationToken);
        }

        var request = new JobEvaluationRequest(
            snapshotFactory.ToJson(candidate),
            snapshotFactory.ToJson(job),
            EvaluationOutputSchema.Json,
            promptVersion.Version);

        var userPrompt = promptBuilder.BuildUserPrompt(promptVersion.Template, request);

        AiCompletion completion;
        try
        {
            completion = await provider.CompleteAsync(
                new AiCompletionRequest(
                    provider.Model ?? "default",
                    EvaluationPromptBuilder.SystemPrompt,
                    userPrompt,
                    MaxOutputTokens: 2500,
                    Temperature: 0.1),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "AI provider call failed; falling back to the rule engine.");
            return await FallbackAsync(candidate, job, "AI provider call failed", cancellationToken);
        }

        try
        {
            var payload = Parse(completion.Content);
            return validator.Validate(payload, job, completion.Provider, completion.Model, promptVersion.Version);
        }
        catch (Exception exception) when (exception is JsonException or AiEvaluationValidationException)
        {
            logger.LogWarning(
                exception,
                "AI evaluation response was rejected; falling back to the rule engine.");
            return await FallbackAsync(candidate, job, $"AI response rejected: {exception.Message}", cancellationToken);
        }
    }

    public static AiEvaluationPayload? Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new AiEvaluationValidationException("The AI response was empty.");
        }

        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            throw new AiEvaluationValidationException("The AI response did not contain a JSON object.");
        }

        var json = content[start..(end + 1)];
        return JsonSerializer.Deserialize<AiEvaluationPayload>(json, JsonOptions);
    }

    private async Task<JobEvaluationDraft> FallbackAsync(
        CandidateProfileSnapshot candidate,
        JobOpportunitySnapshot job,
        string reason,
        CancellationToken cancellationToken)
    {
        var draft = await fallback.EvaluateAsync(candidate, job, cancellationToken);
        var assumptions = draft.Assumptions
            .Append($"Avaliação determinística utilizada: a avaliação por IA não estava disponível ({reason}).")
            .ToArray();

        return draft with { Assumptions = assumptions };
    }
}
