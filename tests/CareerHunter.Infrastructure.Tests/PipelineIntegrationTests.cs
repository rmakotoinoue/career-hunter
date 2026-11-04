using CareerHunter.Application.Abstractions;
using CareerHunter.Application.Assessment;
using CareerHunter.Application.Candidates;
using CareerHunter.Application.Catalog;
using CareerHunter.Application.Common;
using CareerHunter.Application.Discovery;
using CareerHunter.Application.Tracking;
using CareerHunter.Domain.Assessment;
using CareerHunter.Domain.Candidates;
using CareerHunter.Domain.Discovery;
using CareerHunter.Domain.SharedKernel;
using CareerHunter.Domain.Tracking;
using CareerHunter.Infrastructure.Persistence;
using CareerHunter.Infrastructure.Queries;
using CareerHunter.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CareerHunter.Infrastructure.Tests;

public sealed class PipelineIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Manual_source_record_travels_through_the_whole_pipeline_and_never_applies_automatically()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;

        var clock = new FixedClock(Now);

        // Candidate: only facts declared in this test are used.
        var candidates = new CandidateProfileRepository(db);
        var createProfile = new CreateCandidateProfileUseCase(candidates, db, clock);
        var candidateId = await createProfile.ExecuteAsync(new CreateCandidateProfileCommand(
            "Rafael Makoto Inoue",
            "rafael.inoue@example.com",
            "Engenheiro de software sênior",
            "Modernização de sistemas .NET.",
            "São Paulo",
            "SP",
            "Brasil",
            YearsOfExperience: 15,
            LinkedInUrl: null,
            AcceptsRemote: true,
            AcceptsHybrid: true,
            AcceptsOnsite: false,
            AcceptsClt: true,
            AcceptsPj: true,
            ExpectedMinAmount: 9000,
            ExpectedMaxAmount: 12000));

        var addSkill = new AddCandidateSkillUseCase(candidates, db, clock);
        await addSkill.ExecuteAsync(new AddCandidateSkillCommand(candidateId, ".NET", SkillCategory.Technical, 5, 15));
        await addSkill.ExecuteAsync(new AddCandidateSkillCommand(candidateId, "AWS", SkillCategory.Technical, 4, 6));

        // Manual source and one submitted record through the same provenance path as a feed.
        var jobSources = new JobSourceRepository(db);
        var records = new SourceJobRecordRepository(db);
        var runs = new CollectionRunRepository(db);

        var registerSource = new RegisterJobSourceUseCase(jobSources, new CandidateProfileRepository(db), db, clock);
        var sourceId = await registerSource.ExecuteAsync(new RegisterJobSourceCommand(
            "Manual",
            "https://example.com/jobs",
            JobSourceKind.Manual,
            "Only publicly visible postings entered by the candidate.",
            MinimumIntervalMinutes: 60));

        var submit = new SubmitManualJobRecordUseCase(jobSources, records, runs, db, clock);
        var recordId = await submit.ExecuteAsync(new SubmitManualJobRecordCommand(
            sourceId,
            "external-1",
            "https://example.com/jobs/1",
            "Engenheiro .NET Sênior",
            "Empresa Exemplo",
            "Remoto",
            """
            Requisitos:
            .NET e AWS
            Diferenciais:
            Kubernetes

            Contratação CLT, 100% remoto. Faixa: R$ 10.000 - R$ 12.000 por mês.
            """));

        Assert.NotEqual(Guid.Empty, recordId);

        var normalize = new NormalizeSourceRecordsUseCase(
            new EmployerRepository(db),
            new JobOpportunityRepository(db),
            records,
            jobSources,
            db,
            clock,
            new JobNormalizer(new JobRequirementExtractor()),
            NullLogger<NormalizeSourceRecordsUseCase>.Instance);

        var normalization = await normalize.ExecuteAsync(100);
        Assert.Equal(1, normalization.Processed);
        Assert.Equal(1, normalization.Normalized);

        var jobs = new JobOpportunityRepository(db);
        var jobQueries = new JobOpportunityQueries(db);
        var search = await jobQueries.SearchAsync(new JobSearchFilter());
        var jobSummary = Assert.Single(search.Items);
        var jobId = jobSummary.Id;

        // Regression: the fields parsed during normalization must be persisted on the *first*
        // normalization, not only when an existing opportunity is updated.
        Assert.Equal(WorkModality.Remote, jobSummary.Modality);
        Assert.Equal(EmploymentContractType.Clt, jobSummary.ContractType);
        Assert.Equal(SeniorityLevel.Senior, jobSummary.Seniority);
        Assert.Equal(10000m, jobSummary.MinAmount);
        Assert.Equal(12000m, jobSummary.MaxAmount);
        Assert.Equal("BRL", jobSummary.Currency);

        // Evaluation with the deterministic engine: no AI credential required.
        var aiProvider = new UnconfiguredAiProvider();
        var snapshots = new EvaluationSnapshotFactory();
        var ruleEngine = new RuleBasedEvaluationEngine();
        var aiEngine = new AiEvaluationEngine(
            aiProvider,
            new EvaluationPromptVersionRepository(db),
            snapshots,
            new EvaluationPromptBuilder(),
            new JobEvaluationResultValidator(),
            ruleEngine,
            NullLogger<AiEvaluationEngine>.Instance);

        var evaluate = new EvaluateJobOpportunityUseCase(
            candidates,
            jobs,
            new EmployerRepository(db),
            new JobEvaluationRepository(db),
            new EvaluationPromptVersionRepository(db),
            aiProvider,
            snapshots,
            ruleEngine,
            aiEngine,
            db,
            clock,
            NullLogger<EvaluateJobOpportunityUseCase>.Instance);

        var evaluationId = await evaluate.ExecuteAsync(new EvaluateJobOpportunityCommand(
            candidateId,
            jobId,
            EvaluationEnginePreference.RuleBased));

        var evaluationQueries = new JobEvaluationQueries(db);
        var evaluation = await evaluationQueries.GetAsync(evaluationId);
        Assert.NotNull(evaluation);
        Assert.Equal(EvaluationEngine.RuleBased, evaluation!.Engine);
        Assert.True(evaluation.OverallScore >= 55, $"Expected a usable score, got {evaluation.OverallScore}.");

        // A repeat request with the same inputs is served from the stored evaluation: the engine
        // (and, for the AI path, the paid provider call) is not invoked again.
        var reusedEvaluationId = await evaluate.ExecuteAsync(new EvaluateJobOpportunityCommand(
            candidateId,
            jobId,
            EvaluationEnginePreference.RuleBased));

        Assert.Equal(evaluationId, reusedEvaluationId);
        Assert.Equal(1, await db.JobEvaluations.CountAsync(item => item.JobOpportunityId == jobId));

        // Application tracking starts as Discovered and requires human confirmation to reach Applied.
        var applications = new JobApplicationRepository(db);
        var evaluations = new JobEvaluationRepository(db);
        var employers = new EmployerRepository(db);

        var start = new StartApplicationUseCase(candidates, jobs, employers, evaluations, applications, db, clock);
        var applicationId = await start.ExecuteAsync(new StartApplicationCommand(candidateId, jobId, evaluationId));

        var prepare = new PrepareApplicationPackageUseCase(applications, candidates, jobs, employers, evaluations, db, clock);
        var materialIds = await prepare.ExecuteAsync(new PrepareApplicationPackageCommand(applicationId));
        Assert.Equal(2, materialIds.Count);

        var setSalary = new SetApplicationSalaryExpectationUseCase(applications, db, clock);
        await setSalary.ExecuteAsync(new SetApplicationSalaryExpectationCommand(applicationId, 10000, "BRL"));

        var changeStatus = new ChangeApplicationStatusUseCase(applications, evaluations, db, clock);
        await changeStatus.ExecuteAsync(new ChangeApplicationStatusCommand(applicationId, ApplicationStatus.Analyzed, "candidate", "Evaluation ready."));
        await changeStatus.ExecuteAsync(new ChangeApplicationStatusCommand(applicationId, ApplicationStatus.ReadyForReview, "candidate"));
        await changeStatus.ExecuteAsync(new ChangeApplicationStatusCommand(applicationId, ApplicationStatus.Prepared, "candidate"));

        await Assert.ThrowsAsync<DomainException>(() => changeStatus.ExecuteAsync(
            new ChangeApplicationStatusCommand(applicationId, ApplicationStatus.Applied, "candidate", UserConfirmed: false)));

        var applied = await changeStatus.ExecuteAsync(
            new ChangeApplicationStatusCommand(applicationId, ApplicationStatus.Applied, "candidate", UserConfirmed: true));

        Assert.Equal(ApplicationStatus.Applied, applied);

        var applicationQueries = new JobApplicationQueries(db);
        var application = await applicationQueries.GetAsync(applicationId);
        Assert.NotNull(application);
        Assert.NotNull(application!.AppliedAt);
        Assert.Equal(2, application.Materials.Count);
        Assert.Contains(application.StatusHistory, change => change.ToStatus == ApplicationStatus.Applied);
    }

    [Fact]
    public async Task Recollecting_a_feed_whose_item_content_changed_is_skipped_instead_of_conflicting()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var clock = new FixedClock(Now);

        var jobSources = new JobSourceRepository(db);
        var records = new SourceJobRecordRepository(db);
        var runs = new CollectionRunRepository(db);

        var sourceId = await new RegisterJobSourceUseCase(jobSources, new CandidateProfileRepository(db), db, clock).ExecuteAsync(
            new RegisterJobSourceCommand(
                "Feed",
                "https://example.com/feed.rss",
                JobSourceKind.Rss,
                "Public feed, anonymous GET only.",
                MinimumIntervalMinutes: 60));

        var adapter = new StubJobSourceAdapter(new CollectedJob(
            "job-1",
            "https://example.com/jobs/1",
            "Engenheiro .NET Sênior",
            "Empresa Exemplo",
            "Remoto",
            "Requisitos: .NET e AWS. Contratação CLT, 100% remoto.",
            Now));

        var collect = new CollectJobsUseCase(
            jobSources,
            records,
            runs,
            new CandidateProfileRepository(db),
            [adapter],
            db,
            clock,
            NullLogger<CollectJobsUseCase>.Instance);

        var first = Assert.Single(await collect.ExecuteAsync(sourceId, force: true));
        Assert.Equal(1, first.RecordsCollected);
        Assert.Equal(0, first.RecordsSkipped);

        // The feed edits the posting: same external id, different content, therefore a new hash.
        adapter.Current = adapter.Current with
        {
            Title = "Engenheiro .NET Sênior (PJ)",
            Company = "Empresa Exemplo LTDA",
            Description = "Requisitos: .NET, AWS e Azure. Contratação PJ, 100% remoto."
        };

        var second = Assert.Single(await collect.ExecuteAsync(sourceId, force: true));
        Assert.Equal(0, second.RecordsCollected);
        Assert.Equal(1, second.RecordsSkipped);
        Assert.Equal(1, await db.SourceJobRecords.CountAsync());
    }

    private sealed class StubJobSourceAdapter(CollectedJob job) : IJobSourceAdapter
    {
        public CollectedJob Current { get; set; } = job;

        public JobSourceKind Kind => JobSourceKind.Rss;

        public Task<IReadOnlyList<CollectedJob>> CollectAsync(
            JobSource source,
            CareerHunter.Domain.Candidates.JobSearchPlan? searchPlan = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CollectedJob>>([Current]);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class UnconfiguredAiProvider : IAIProvider
    {
        public bool IsConfigured => false;

        public string? Model => null;

        public Task<AiCompletion> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("AI provider is not configured in this test.");
    }
}
