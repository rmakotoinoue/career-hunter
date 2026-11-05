using CareerHunter.Application.Abstractions;
using CareerHunter.Application.Assessment;
using CareerHunter.Application.Candidates;
using CareerHunter.Application.Catalog;
using CareerHunter.Application.Common;
using CareerHunter.Application.Discovery;
using CareerHunter.Application.Notifications;
using CareerHunter.Domain.Assessment;
using CareerHunter.Domain.Candidates;
using CareerHunter.Domain.Discovery;
using CareerHunter.Domain.SharedKernel;
using CareerHunter.Infrastructure.Persistence;
using CareerHunter.Infrastructure.Queries;
using CareerHunter.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CareerHunter.Infrastructure.Tests;

/// <summary>
/// Read-side contract for the dashboard: one request returns every digest item joined with
/// the stored job detail, and an unknown digest is distinguishable from an empty one.
/// </summary>
public sealed class DigestQueriesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Digest_items_are_joined_with_the_stored_job_detail_in_one_query_per_aggregate()
    {
        var (database, digestId) = await BuildDigestAsync();
        await using (database)
        {
            var queries = new DailyDigestQueries(database.Context);

            var items = await queries.ListItemsAsync(digestId);

            Assert.NotNull(items);
            var item = Assert.Single(items!);

            // Digest identity: the list row keeps its own rank, score and original URL.
            Assert.Equal(1, item.Rank);
            Assert.Equal("Engenheiro .NET Sênior", item.JobTitle);
            Assert.Equal("Empresa Exemplo", item.EmployerName);
            Assert.Equal("https://example.com/jobs/1", item.JobUrl);
            Assert.True(item.Score >= 50, $"Expected the minimum score to be met, got {item.Score}.");

            // Job detail the dashboard renders without extra calls.
            Assert.NotEmpty(item.Description);
            Assert.Equal(WorkModality.Remote, item.Modality);
            Assert.Equal(EmploymentContractType.Clt, item.ContractType);
            Assert.Equal(SeniorityLevel.Senior, item.Seniority);
            Assert.NotNull(item.Compensation);
            Assert.Equal(10000m, item.Compensation!.MinAmount);
            Assert.Equal(12000m, item.Compensation.MaxAmount);
            Assert.Equal("BRL", item.Compensation.Currency);
            Assert.Contains(item.Requirements, requirement => requirement.Name.Contains(".NET"));
            Assert.Equal("Manual", item.SourceName);
        }
    }

    [Fact]
    public async Task Unknown_digest_returns_null_so_the_api_can_answer_404_instead_of_an_empty_list()
    {
        var (database, _) = await BuildDigestAsync();
        await using (database)
        {
            var queries = new DailyDigestQueries(database.Context);

            Assert.Null(await queries.ListItemsAsync(Guid.NewGuid()));
        }
    }

    [Fact]
    public async Task A_digest_without_items_returns_an_empty_list_not_null()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var clock = new FixedClock(Now);

        var candidates = new CandidateProfileRepository(db);
        var candidateId = await new CreateCandidateProfileUseCase(candidates, db, clock).ExecuteAsync(
            new CreateCandidateProfileCommand(
                "Candidato Teste",
                "candidato@example.com",
                "Engenheiro de software",
                "Teste.",
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

        var preferences = new NotificationPreferenceRepository(db);
        await new ConfigureNotificationPreferenceUseCase(preferences, candidates, db, clock).ExecuteAsync(
            new ConfigureNotificationPreferenceCommand(
                candidateId,
                "candidato@example.com",
                IsEnabled: true,
                SendHourLocal: 8,
                TimeZoneId: "America/Sao_Paulo",
                MinimumScore: 50));

        // No evaluations on this date: the digest exists and is legitimately empty.
        var generation = await new GenerateDailyDigestUseCase(
            preferences,
            new DailyDigestRepository(db),
            new JobEvaluationQueries(db),
            db,
            clock).ExecuteAsync(new GenerateDailyDigestCommand(candidateId));

        Assert.Equal(0, generation.ItemCount);

        var items = await new DailyDigestQueries(db).ListItemsAsync(generation.DigestId);

        Assert.NotNull(items);
        Assert.Empty(items!);
    }

    /// <summary>
    /// One candidate, one manual record through normalization and evaluation, then the digest
    /// of the day. Same path the worker runs, without any network access.
    /// </summary>
    private static async Task<(SqliteTestDatabase Database, Guid DigestId)> BuildDigestAsync()
    {
        var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var clock = new FixedClock(Now);

        try
        {
            var candidates = new CandidateProfileRepository(db);
            var candidateId = await new CreateCandidateProfileUseCase(candidates, db, clock).ExecuteAsync(
                new CreateCandidateProfileCommand(
                    "Rafael Makoto Inoue",
                    "rafael@example.com",
                    "Engenheiro de software sênior",
                    "Modernização de sistemas .NET.",
                    "São Paulo",
                    "SP",
                    "Brasil",
                    YearsOfExperience: 19,
                    LinkedInUrl: null,
                    AcceptsRemote: true,
                    AcceptsHybrid: true,
                    AcceptsOnsite: false,
                    AcceptsClt: true,
                    AcceptsPj: true,
                    ExpectedMinAmount: 9000,
                    ExpectedMaxAmount: 12000));

            await new AddCandidateSkillUseCase(candidates, db, clock).ExecuteAsync(
                new AddCandidateSkillCommand(candidateId, ".NET", SkillCategory.Technical, 5, 15));

            var jobSources = new JobSourceRepository(db);
            var records = new SourceJobRecordRepository(db);
            var runs = new CollectionRunRepository(db);

            var sourceId = await new RegisterJobSourceUseCase(jobSources, candidates, db, clock).ExecuteAsync(
                new RegisterJobSourceCommand(
                    "Manual",
                    "https://example.com/jobs",
                    JobSourceKind.Manual,
                    "Public postings entered by the candidate.",
                    MinimumIntervalMinutes: 60));

            await new SubmitManualJobRecordUseCase(jobSources, records, runs, db, clock).ExecuteAsync(
                new SubmitManualJobRecordCommand(
                    sourceId,
                    "manual-1",
                    "https://example.com/jobs/1",
                    "Engenheiro .NET Sênior",
                    "Empresa Exemplo",
                    "Remoto",
                    """
                    Requisitos:
                    .NET e AWS

                    Contratação CLT, 100% remoto. Faixa: R$ 10.000 - R$ 12.000 por mês.
                    """));

            var normalization = await new NormalizeSourceRecordsUseCase(
                new EmployerRepository(db),
                new JobOpportunityRepository(db),
                records,
                jobSources,
                db,
                clock,
                new JobNormalizer(new JobRequirementExtractor()),
                NullLogger<NormalizeSourceRecordsUseCase>.Instance).ExecuteAsync(100);

            Assert.Equal(1, normalization.Normalized);

            var jobs = new JobOpportunityRepository(db);
            var job = Assert.Single(await db.JobOpportunities.AsNoTracking().ToListAsync());

            var aiProvider = new UnconfiguredAiProvider();
            var ruleEngine = new RuleBasedEvaluationEngine();
            var aiEngine = new AiEvaluationEngine(
                aiProvider,
                new EvaluationPromptVersionRepository(db),
                new EvaluationSnapshotFactory(),
                new EvaluationPromptBuilder(),
                new JobEvaluationResultValidator(),
                ruleEngine,
                NullLogger<AiEvaluationEngine>.Instance);

            await new EvaluateJobOpportunityUseCase(
                candidates,
                jobs,
                new EmployerRepository(db),
                new JobEvaluationRepository(db),
                new EvaluationPromptVersionRepository(db),
                aiProvider,
                new EvaluationSnapshotFactory(),
                ruleEngine,
                aiEngine,
                db,
                clock,
                NullLogger<EvaluateJobOpportunityUseCase>.Instance).ExecuteAsync(
                new EvaluateJobOpportunityCommand(candidateId, job.Id, EvaluationEnginePreference.RuleBased));

            var preferences = new NotificationPreferenceRepository(db);
            await new ConfigureNotificationPreferenceUseCase(preferences, candidates, db, clock).ExecuteAsync(
                new ConfigureNotificationPreferenceCommand(
                    candidateId,
                    "rafael@example.com",
                    IsEnabled: true,
                    SendHourLocal: 8,
                    TimeZoneId: "America/Sao_Paulo",
                    MinimumScore: 50));

            var generation = await new GenerateDailyDigestUseCase(
                preferences,
                new DailyDigestRepository(db),
                new JobEvaluationQueries(db),
                db,
                clock).ExecuteAsync(new GenerateDailyDigestCommand(candidateId));

            Assert.False(generation.AlreadyExisted);
            Assert.Equal(1, generation.ItemCount);

            return (database, generation.DigestId);
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
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
