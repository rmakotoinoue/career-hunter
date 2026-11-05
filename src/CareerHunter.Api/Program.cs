using System.Text.Json.Serialization;
using CareerHunter.Api;
using CareerHunter.Application;
using CareerHunter.Application.Abstractions;
using CareerHunter.Application.Assessment;
using CareerHunter.Application.Candidates;
using CareerHunter.Application.Catalog;
using CareerHunter.Application.Discovery;
using CareerHunter.Application.Notifications;
using CareerHunter.Application.Tracking;
using CareerHunter.Domain.Candidates;
using CareerHunter.Domain.SharedKernel;
using CareerHunter.Domain.Tracking;
using CareerHunter.Infrastructure;
using CareerHunter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var apiKeyOptions = new ApiKeyOptions { ApiKey = builder.Configuration["Api:ApiKey"] };
builder.Services.AddSingleton(apiKeyOptions);
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.UseExceptionHandler();

if (!apiKeyOptions.IsEnabled)
{
    app.Logger.LogWarning(
        "Api:ApiKey is not configured. Every /api endpoint is reachable without authentication; " +
        "only do this for a local run bound to the loopback interface.");
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

await using (var scope = app.Services.CreateAsyncScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

app.MapGet("/", () => Results.Ok(new
{
    application = "Career Hunter API",
    version = "1.0.0",
    policy = "Opportunities are only analyzed and prepared. Applications are always reviewed and submitted by the candidate."
}));

app.MapHealthChecks("/health");

app.MapGet("/health/database", async (
    IServiceScopeFactory scopeFactory,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    await using var scope = scopeFactory.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<CareerHunterDbContext>();

    try
    {
        if (await db.Database.CanConnectAsync(cancellationToken))
        {
            return Results.Ok(new { database = "available" });
        }

        return Results.Problem("Database connectivity check failed.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception exception)
    {
        loggerFactory.CreateLogger("Health.Database").LogWarning(exception, "Database health check failed.");
        return Results.Problem("Database connectivity check failed.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

var api = app.MapGroup("/api").AddEndpointFilter<ApiKeyEndpointFilter>();

// ---------------------------------------------------------------- Candidate profile
var candidates = api.MapGroup("/candidates").WithTags("Candidates");

candidates.MapGet("", async (ICandidateProfileQueries queries, CancellationToken cancellationToken) =>
    Results.Ok(await queries.ListAsync(cancellationToken)));

candidates.MapGet("/{id:guid}", async (Guid id, ICandidateProfileQueries queries, CancellationToken cancellationToken) =>
    await queries.GetAsync(id, cancellationToken) is { } profile
        ? Results.Ok(profile)
        : NotFound("CandidateProfile"));

candidates.MapPost("", async (
    CreateCandidateProfileCommand command,
    CreateCandidateProfileUseCase useCase,
    CancellationToken cancellationToken) =>
{
    var id = await useCase.ExecuteAsync(command, cancellationToken);
    return Results.Created($"/api/candidates/{id}", new { id });
});

candidates.MapPut("/{id:guid}", async (
    Guid id,
    UpdateCandidateProfileCommand command,
    UpdateCandidateProfileUseCase useCase,
    CancellationToken cancellationToken) =>
{
    await useCase.ExecuteAsync(command with { Id = id }, cancellationToken);
    return Results.NoContent();
});

candidates.MapPut("/{id:guid}/preferences", async (
    Guid id,
    UpdateWorkPreferencesCommand command,
    UpdateWorkPreferencesUseCase useCase,
    CancellationToken cancellationToken) =>
{
    await useCase.ExecuteAsync(command with { CandidateProfileId = id }, cancellationToken);
    return Results.NoContent();
});

candidates.MapPost("/{id:guid}/experiences", async (
    Guid id,
    AddCandidateExperienceCommand command,
    AddCandidateExperienceUseCase useCase,
    CancellationToken cancellationToken) =>
{
    var experienceId = await useCase.ExecuteAsync(command with { CandidateProfileId = id }, cancellationToken);
    return Results.Created($"/api/candidates/{id}/experiences/{experienceId}", new { id = experienceId });
});

candidates.MapPost("/{id:guid}/skills", async (
    Guid id,
    AddCandidateSkillCommand command,
    AddCandidateSkillUseCase useCase,
    CancellationToken cancellationToken) =>
{
    var skillId = await useCase.ExecuteAsync(command with { CandidateProfileId = id }, cancellationToken);
    return Results.Created($"/api/candidates/{id}/skills/{skillId}", new { id = skillId });
});

candidates.MapDelete("/{id:guid}/skills/{skillId:guid}", async (
    Guid id,
    Guid skillId,
    RemoveCandidateSkillUseCase useCase,
    CancellationToken cancellationToken) =>
{
    await useCase.ExecuteAsync(new RemoveCandidateSkillCommand(id, skillId), cancellationToken);
    return Results.NoContent();
});

candidates.MapPost("/{id:guid}/educations", async (
    Guid id,
    AddCandidateEducationCommand command,
    AddCandidateEducationUseCase useCase,
    CancellationToken cancellationToken) =>
{
    var educationId = await useCase.ExecuteAsync(command with { CandidateProfileId = id }, cancellationToken);
    return Results.Created($"/api/candidates/{id}/educations/{educationId}", new { id = educationId });
});

candidates.MapPost("/{id:guid}/certifications", async (
    Guid id,
    AddCandidateCertificationCommand command,
    AddCandidateCertificationUseCase useCase,
    CancellationToken cancellationToken) =>
{
    var certificationId = await useCase.ExecuteAsync(command with { CandidateProfileId = id }, cancellationToken);
    return Results.Created($"/api/candidates/{id}/certifications/{certificationId}", new { id = certificationId });
});

candidates.MapPost("/{id:guid}/career-targets", async (
    Guid id,
    AddCareerTargetCommand command,
    AddCareerTargetUseCase useCase,
    CancellationToken cancellationToken) =>
{
    var targetId = await useCase.ExecuteAsync(command with { CandidateProfileId = id }, cancellationToken);
    return Results.Created($"/api/candidates/{id}/career-targets/{targetId}", new { id = targetId });
});

candidates.MapPut("/{id:guid}/career-targets/{targetId:guid}/primary", async (
    Guid id,
    Guid targetId,
    SetPrimaryCareerTargetUseCase useCase,
    CancellationToken cancellationToken) =>
{
    await useCase.ExecuteAsync(new SetPrimaryCareerTargetCommand(id, targetId), cancellationToken);
    return Results.NoContent();
});

// ---------------------------------------------------------------- Discovery
var sources = api.MapGroup("/job-sources").WithTags("Discovery");

sources.MapGet("", async (IDiscoveryQueries queries, CancellationToken cancellationToken) =>
    Results.Ok(await queries.ListSourcesAsync(cancellationToken)));

sources.MapPost("", async (
    RegisterJobSourceCommand command,
    RegisterJobSourceUseCase useCase,
    CancellationToken cancellationToken) =>
{
    var id = await useCase.ExecuteAsync(command, cancellationToken);
    return Results.Created($"/api/job-sources/{id}", new { id });
});

sources.MapPut("/{id:guid}/enabled", async (
    Guid id,
    SetJobSourceEnabledCommand command,
    SetJobSourceEnabledUseCase useCase,
    CancellationToken cancellationToken) =>
{
    await useCase.ExecuteAsync(command with { JobSourceId = id }, cancellationToken);
    return Results.NoContent();
}).WithSummary("Pauses or resumes a job source; disabled sources are skipped by the worker.");

sources.MapPost("/collect", async (
    Guid? sourceId,
    Guid? candidateProfileId,
    bool? force,
    CollectJobsUseCase useCase,
    CancellationToken cancellationToken) =>
    Results.Ok(await useCase.ExecuteAsync(sourceId, force ?? false, candidateProfileId, cancellationToken)))
    .WithSummary("Collects enabled sources. With candidateProfileId, only that candidate's sources run and the search is rebuilt from the profile.");

sources.MapGet("/runs", async (
    Guid? sourceId,
    int? take,
    IDiscoveryQueries queries,
    CancellationToken cancellationToken) =>
    Results.Ok(await queries.ListRunsAsync(sourceId, Math.Clamp(take ?? 20, 1, 200), cancellationToken)));

sources.MapGet("/records", async (
    Guid? sourceId,
    CareerHunter.Domain.Discovery.SourceJobRecordStatus? status,
    int? take,
    IDiscoveryQueries queries,
    CancellationToken cancellationToken) =>
    Results.Ok(await queries.ListRecordsAsync(sourceId, status, Math.Clamp(take ?? 50, 1, 500), cancellationToken)));

sources.MapPost("/{id:guid}/records", async (
    Guid id,
    SubmitManualJobRecordCommand command,
    SubmitManualJobRecordUseCase useCase,
    CancellationToken cancellationToken) =>
{
    var recordId = await useCase.ExecuteAsync(command with { JobSourceId = id }, cancellationToken);
    return Results.Created($"/api/job-sources/{id}/records/{recordId}", new { id = recordId });
});

api.MapPost("/catalog/normalize", async (
    int? take,
    NormalizeSourceRecordsUseCase useCase,
    CancellationToken cancellationToken) =>
    Results.Ok(await useCase.ExecuteAsync(take ?? 100, cancellationToken)))
    .WithTags("Catalog");

// ---------------------------------------------------------------- Job catalog
var jobs = api.MapGroup("/jobs").WithTags("Jobs");

jobs.MapGet("", async (
    string? query,
    WorkModality? modality,
    EmploymentContractType? contractType,
    SeniorityLevel? seniority,
    bool? onlyActive,
    int? page,
    int? pageSize,
    IJobOpportunityQueries queries,
    CancellationToken cancellationToken) =>
{
    var filter = new JobSearchFilter(
        query,
        modality,
        contractType,
        seniority,
        onlyActive ?? true,
        page ?? 1,
        pageSize ?? 20);

    return Results.Ok(await queries.SearchAsync(filter, cancellationToken));
});

jobs.MapGet("/{id:guid}", async (Guid id, IJobOpportunityQueries queries, CancellationToken cancellationToken) =>
    await queries.GetAsync(id, cancellationToken) is { } job
        ? Results.Ok(job)
        : NotFound("JobOpportunity"));

jobs.MapGet("/{id:guid}/evaluations", async (
    Guid id,
    IJobEvaluationQueries queries,
    CancellationToken cancellationToken) =>
    Results.Ok(await queries.ListByJobAsync(id, cancellationToken)));

// ---------------------------------------------------------------- Assessment
var evaluations = api.MapGroup("/evaluations").WithTags("Assessment");

evaluations.MapPost("", async (
    EvaluateJobOpportunityCommand command,
    EvaluateJobOpportunityUseCase useCase,
    CancellationToken cancellationToken) =>
{
    var id = await useCase.ExecuteAsync(command, cancellationToken);
    return Results.Created($"/api/evaluations/{id}", new { id });
});

evaluations.MapGet("/{id:guid}", async (
    Guid id,
    IJobEvaluationQueries queries,
    CancellationToken cancellationToken) =>
    await queries.GetAsync(id, cancellationToken) is { } evaluation
        ? Results.Ok(evaluation)
        : NotFound("JobEvaluation"));

// ---------------------------------------------------------------- Application tracking
var applications = api.MapGroup("/applications").WithTags("Applications");

applications.MapGet("", async (
    Guid? candidateProfileId,
    ApplicationStatus? status,
    IApplicationQueries queries,
    CancellationToken cancellationToken) =>
    Results.Ok(await queries.ListAsync(candidateProfileId, status, cancellationToken)));

applications.MapGet("/{id:guid}", async (
    Guid id,
    IApplicationQueries queries,
    CancellationToken cancellationToken) =>
    await queries.GetAsync(id, cancellationToken) is { } application
        ? Results.Ok(application)
        : NotFound("Application"));

applications.MapPost("", async (
    StartApplicationCommand command,
    StartApplicationUseCase useCase,
    CancellationToken cancellationToken) =>
{
    var id = await useCase.ExecuteAsync(command, cancellationToken);
    return Results.Created($"/api/applications/{id}", new { id });
});

applications.MapPost("/{id:guid}/materials", async (
    Guid id,
    AddApplicationMaterialCommand command,
    AddApplicationMaterialUseCase useCase,
    CancellationToken cancellationToken) =>
{
    var materialId = await useCase.ExecuteAsync(command with { ApplicationId = id }, cancellationToken);
    return Results.Created($"/api/applications/{id}/materials/{materialId}", new { id = materialId });
});

applications.MapPost("/{id:guid}/notes", async (
    Guid id,
    AddApplicationNoteCommand command,
    AddApplicationNoteUseCase useCase,
    CancellationToken cancellationToken) =>
{
    var noteId = await useCase.ExecuteAsync(command with { ApplicationId = id }, cancellationToken);
    return Results.Created($"/api/applications/{id}/notes/{noteId}", new { id = noteId });
});

applications.MapPut("/{id:guid}/salary-expectation", async (
    Guid id,
    SetApplicationSalaryExpectationCommand command,
    SetApplicationSalaryExpectationUseCase useCase,
    CancellationToken cancellationToken) =>
{
    await useCase.ExecuteAsync(command with { ApplicationId = id }, cancellationToken);
    return Results.NoContent();
});

applications.MapPut("/{id:guid}/status", async (
    Guid id,
    ChangeApplicationStatusCommand command,
    ChangeApplicationStatusUseCase useCase,
    CancellationToken cancellationToken) =>
{
    var status = await useCase.ExecuteAsync(command with { ApplicationId = id }, cancellationToken);
    return Results.Ok(new { status });
}).WithSummary("Change the application status. Moving to 'Applied' requires userConfirmed=true; the API never submits anything.");

applications.MapPost("/{id:guid}/prepare-package", async (
    Guid id,
    PrepareApplicationPackageUseCase useCase,
    CancellationToken cancellationToken) =>
{
    var materialIds = await useCase.ExecuteAsync(new PrepareApplicationPackageCommand(id), cancellationToken);
    return Results.Ok(new { materialIds });
}).WithSummary("Builds a resume summary and recruiter message from declared data for manual review.");

// ---------------------------------------------------------------- Notifications
api.MapPut("/candidates/{id:guid}/notification-preference", async (
    Guid id,
    ConfigureNotificationPreferenceCommand command,
    ConfigureNotificationPreferenceUseCase useCase,
    CancellationToken cancellationToken) =>
{
    var preferenceId = await useCase.ExecuteAsync(command with { CandidateProfileId = id }, cancellationToken);
    return Results.Ok(new { id = preferenceId });
}).WithTags("Notifications");

var digests = api.MapGroup("/digests").WithTags("Notifications");

digests.MapGet("", async (
    Guid? candidateProfileId,
    int? take,
    IDailyDigestQueries queries,
    CancellationToken cancellationToken) =>
    Results.Ok(await queries.ListAsync(candidateProfileId, Math.Clamp(take ?? 20, 1, 200), cancellationToken)));

digests.MapGet("/{id:guid}", async (
    Guid id,
    IDailyDigestQueries queries,
    CancellationToken cancellationToken) =>
    await queries.GetAsync(id, cancellationToken) is { } digest
        ? Results.Ok(digest)
        : NotFound("DailyDigest"));

// The dashboard reads this instead of calling /api/jobs once per item.
digests.MapGet("/{id:guid}/items", async (
    Guid id,
    IDailyDigestQueries queries,
    CancellationToken cancellationToken) =>
    await queries.ListItemsAsync(id, cancellationToken) is { } items
        ? Results.Ok(items)
        : NotFound("DailyDigest"))
    .WithSummary("Digest items joined with the stored job detail (description, modality, compensation, requirements, source).");

digests.MapPost("", async (
    GenerateDailyDigestCommand command,
    GenerateDailyDigestUseCase useCase,
    CancellationToken cancellationToken) =>
    Results.Ok(await useCase.ExecuteAsync(command, cancellationToken)));

digests.MapPost("/{id:guid}/send", async (
    Guid id,
    SendDailyDigestUseCase useCase,
    CancellationToken cancellationToken) =>
    Results.Ok(await useCase.ExecuteAsync(new SendDailyDigestCommand(id), cancellationToken)));

// Renders the e-mail exactly as it would be delivered, without sending it. `format` accepts
// html (default, opens in the browser), text or json.
digests.MapGet("/{id:guid}/preview", async (
    Guid id,
    string? format,
    PreviewDailyDigestUseCase useCase,
    CancellationToken cancellationToken) =>
{
    var preview = await useCase.ExecuteAsync(id, cancellationToken);

    return format?.ToLowerInvariant() switch
    {
        "json" => Results.Ok(preview),
        "text" => Results.Text(preview.TextBody, "text/plain; charset=utf-8"),
        _ => Results.Content(
            $"""
             <!doctype html>
             <html lang="pt-BR">
             <head><meta charset="utf-8"><title>{System.Net.WebUtility.HtmlEncode(preview.Subject)}</title></head>
             <body style="font-family:Segoe UI,Arial,sans-serif;max-width:640px;margin:24px auto;color:#222">
             <div style="background:#f4f4f4;padding:8px 12px;border-radius:6px;font-size:13px">
             Prévia — nenhum e-mail foi enviado.<br/>
             Para: {System.Net.WebUtility.HtmlEncode(string.Join(", ", preview.To))}<br/>
             Assunto: {System.Net.WebUtility.HtmlEncode(preview.Subject)}
             </div>
             {preview.HtmlBody}
             </body>
             </html>
             """,
            "text/html; charset=utf-8")
    };
});

app.Run();

static IResult NotFound(string resource)
    => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Resource not found",
        detail: $"{resource} was not found.");

/// <summary>Exposed for API integration tests.</summary>
public partial class Program
{
}
