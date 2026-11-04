using CareerHunter.Application.Abstractions;
using CareerHunter.Application.Assessment;
using CareerHunter.Application.Catalog;
using CareerHunter.Application.Common;
using CareerHunter.Application.Discovery;
using CareerHunter.Application.Notifications;
using CareerHunter.Domain.Notifications;

namespace CareerHunter.Worker;

/// <summary>
/// Runs the daily pipeline: collect raw records, normalize them into opportunities, evaluate
/// them for every registered candidate, then build and send each candidate's digest.
/// Every stage is idempotent and failures are isolated per candidate/source.
/// </summary>
public sealed class JobPipelineWorker(
    IServiceScopeFactory scopeFactory,
    WorkerOptions options,
    ILogger<JobPipelineWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Career Hunter worker started. Cycle: {Cycle} minute(s); AI: {UseAi}; digests: {SendDigests}.",
            options.CycleIntervalMinutes,
            options.UseAi,
            options.SendDigests);

        if (options.RunOnStartup)
        {
            await RunCycleAsync(stoppingToken);
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, options.CycleIntervalMinutes)));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCycleAsync(stoppingToken);
        }
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var services = scope.ServiceProvider;

            await CollectAsync(services, cancellationToken);
            await NormalizeAsync(services, cancellationToken);
            await EvaluateAsync(services, cancellationToken);

            if (options.SendDigests)
            {
                await BuildAndSendDigestsAsync(services, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The background cycle failed; the next cycle will retry.");
        }
    }

    private async Task CollectAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var useCase = services.GetRequiredService<CollectJobsUseCase>();
        var results = await useCase.ExecuteAsync(null, options.CollectForce, cancellationToken: cancellationToken);

        foreach (var result in results)
        {
            logger.LogInformation(
                "Collected source {SourceId} with status {Status}: {Collected} stored, {Skipped} skipped, {Rejected} rejected.",
                result.JobSourceId,
                result.Status,
                result.RecordsCollected,
                result.RecordsSkipped,
                result.RecordsRejected);
        }
    }

    private async Task NormalizeAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var useCase = services.GetRequiredService<NormalizeSourceRecordsUseCase>();
        var result = await useCase.ExecuteAsync(options.NormalizeTake, cancellationToken);

        logger.LogInformation(
            "Normalization processed {Processed} record(s): {Normalized} normalized, {Rejected} rejected.",
            result.Processed,
            result.Normalized,
            result.Rejected);
    }

    private async Task EvaluateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var candidates = await services.GetRequiredService<ICandidateProfileQueries>()
            .ListAsync(cancellationToken);
        var jobs = services.GetRequiredService<IJobOpportunityQueries>();
        var useCase = services.GetRequiredService<EvaluateJobOpportunityUseCase>();

        foreach (var candidate in candidates)
        {
            var pending = await jobs.ListUnevaluatedAsync(candidate.Id, options.EvaluateTake, cancellationToken);

            foreach (var job in pending)
            {
                try
                {
                    var evaluationId = await useCase.ExecuteAsync(
                        new EvaluateJobOpportunityCommand(
                            candidate.Id,
                            job.Id,
                            options.UseAi ? EvaluationEnginePreference.Auto : EvaluationEnginePreference.RuleBased),
                        cancellationToken);

                    logger.LogInformation(
                        "Evaluation {EvaluationId} prepared for candidate {CandidateId} and job {JobId}.",
                        evaluationId,
                        candidate.Id,
                        job.Id);
                }
                catch (BusinessRuleException exception)
                {
                    logger.LogWarning(
                        "Skipping job {JobId} for candidate {CandidateId}: {Reason}",
                        job.Id,
                        candidate.Id,
                        exception.Message);
                }
                catch (NotFoundException exception)
                {
                    logger.LogWarning(exception, "Skipping job {JobId} because related data disappeared.", job.Id);
                }
            }
        }
    }

    private async Task BuildAndSendDigestsAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var preferences = await services.GetRequiredService<INotificationPreferenceRepository>()
            .ListEnabledAsync(cancellationToken);

        var generate = services.GetRequiredService<GenerateDailyDigestUseCase>();
        var send = services.GetRequiredService<SendDailyDigestUseCase>();

        foreach (var preference in preferences)
        {
            if (!IsAtOrAfterSendHour(preference, cancellationToken))
            {
                continue;
            }

            try
            {
                var generated = await generate.ExecuteAsync(
                    new GenerateDailyDigestCommand(preference.CandidateProfileId),
                    cancellationToken);

                if (generated.Status == DigestStatus.Sent)
                {
                    continue;
                }

                var delivery = await send.ExecuteAsync(
                    new SendDailyDigestCommand(generated.DigestId),
                    cancellationToken);

                logger.LogInformation(
                    "Digest {DigestId} for candidate {CandidateId}: {Status} ({Items} item(s), attempt {Attempt}).",
                    generated.DigestId,
                    preference.CandidateProfileId,
                    delivery.Status,
                    generated.ItemCount,
                    delivery.AttemptCount);
            }
            catch (NotFoundException exception)
            {
                logger.LogWarning(exception, "Skipping digest for candidate {CandidateId}.", preference.CandidateProfileId);
            }
        }
    }

    private bool IsAtOrAfterSendHour(NotificationPreference preference, CancellationToken cancellationToken)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(preference.TimeZoneId);
            var localHour = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).Hour;
            return localHour >= preference.SendHourLocal;
        }
        catch (TimeZoneNotFoundException)
        {
            logger.LogWarning(
                "Unknown time zone {TimeZoneId} for candidate {CandidateId}; using {Fallback}.",
                preference.TimeZoneId,
                preference.CandidateProfileId,
                options.TimeZoneId);

            var zone = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId);
            return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).Hour >= preference.SendHourLocal;
        }
    }
}
