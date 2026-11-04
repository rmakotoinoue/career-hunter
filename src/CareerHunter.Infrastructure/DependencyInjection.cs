using CareerHunter.Application.Abstractions;
using CareerHunter.Application.Common;
using CareerHunter.Infrastructure.Adapters;
using CareerHunter.Infrastructure.Options;
using CareerHunter.Infrastructure.Persistence;
using CareerHunter.Infrastructure.Queries;
using CareerHunter.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CareerHunter.Infrastructure;

/// <summary>
/// Composition root for infrastructure adapters. The API and the Worker share it so both
/// processes see exactly the same persistence and integration behaviour.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("CareerHunter");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'CareerHunter' is not configured. Set ConnectionStrings__CareerHunter.");
        }

        services.AddDbContext<CareerHunterDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null));
        });

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<CareerHunterDbContext>());

        AddRepositories(services);
        AddQueries(services);

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<DatabaseInitializer>();

        var aiOptions = ReadAiOptions(configuration);
        services.AddSingleton(aiOptions);
        services.AddHttpClient<OpenAiProvider>((_, client) =>
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, aiOptions.TimeoutSeconds)));
        services.AddScoped<IAIProvider>(provider => provider.GetRequiredService<OpenAiProvider>());

        var emailOptions = ReadEmailOptions(configuration);
        services.AddSingleton(emailOptions);
        services.AddSingleton<IEmailSender, EmailSender>();

        services.AddHttpClient("job-sources", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CareerHunter/1.0 (+personal job research)");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        var providerOptions = ReadJobProviderOptions(configuration);
        services.AddSingleton(providerOptions);
        services.AddSingleton(new JobPostingFilter(providerOptions.Filter));

        services.AddSingleton<IJobSourceAdapter, RssJobSourceAdapter>();
        services.AddSingleton<IJobSourceAdapter, ManualJobSourceAdapter>();
        services.AddSingleton<IJobSourceAdapter, AdzunaJobSourceAdapter>();
        services.AddSingleton<IJobSourceAdapter, JoobleJobSourceAdapter>();
        services.AddSingleton<IJobSourceAdapter, GreenhouseJobSourceAdapter>();
        services.AddSingleton<IJobSourceAdapter, LeverJobSourceAdapter>();

        return services;
    }

    private static void AddRepositories(IServiceCollection services)
    {
        services.AddScoped<ICandidateProfileRepository, CandidateProfileRepository>();
        services.AddScoped<IJobSourceRepository, JobSourceRepository>();
        services.AddScoped<ISourceJobRecordRepository, SourceJobRecordRepository>();
        services.AddScoped<ICollectionRunRepository, CollectionRunRepository>();
        services.AddScoped<IEmployerRepository, EmployerRepository>();
        services.AddScoped<IJobOpportunityRepository, JobOpportunityRepository>();
        services.AddScoped<IJobEvaluationRepository, JobEvaluationRepository>();
        services.AddScoped<IEvaluationPromptVersionRepository, EvaluationPromptVersionRepository>();
        services.AddScoped<IJobApplicationRepository, JobApplicationRepository>();
        services.AddScoped<INotificationPreferenceRepository, NotificationPreferenceRepository>();
        services.AddScoped<IDailyDigestRepository, DailyDigestRepository>();
    }

    private static void AddQueries(IServiceCollection services)
    {
        services.AddScoped<ICandidateProfileQueries, CandidateProfileQueries>();
        services.AddScoped<IJobOpportunityQueries, JobOpportunityQueries>();
        services.AddScoped<IJobEvaluationQueries, JobEvaluationQueries>();
        services.AddScoped<IApplicationQueries, JobApplicationQueries>();
        services.AddScoped<IDiscoveryQueries, DiscoveryQueries>();
        services.AddScoped<IDailyDigestQueries, DailyDigestQueries>();
    }

    private static AiOptions ReadAiOptions(IConfiguration configuration)
    {
        var section = configuration.GetSection("Ai");

        return new AiOptions
        {
            BaseUrl = Text(section, "BaseUrl"),
            ApiKey = Text(section, "ApiKey"),
            Model = Text(section, "Model"),
            UseJsonResponseFormat = Boolean(section, "UseJsonResponseFormat", fallback: true),
            TimeoutSeconds = Integer(section, "TimeoutSeconds", fallback: 60)
        };
    }

    private static EmailOptions ReadEmailOptions(IConfiguration configuration)
    {
        var section = configuration.GetSection("Email");

        return new EmailOptions
        {
            Host = Text(section, "Host"),
            Port = Integer(section, "Port", fallback: 587),
            UseStartTls = Boolean(section, "UseStartTls", fallback: true),
            UserName = Text(section, "UserName"),
            Password = Text(section, "Password"),
            FromAddress = Text(section, "FromAddress"),
            FromName = Text(section, "FromName") ?? "Career Hunter",
            TimeoutSeconds = Integer(section, "TimeoutSeconds", fallback: 30)
        };
    }

    private static JobProviderOptions ReadJobProviderOptions(IConfiguration configuration)
    {
        var section = configuration.GetSection("JobProviders");
        var adzuna = section.GetSection("Adzuna");
        var jooble = section.GetSection("Jooble");
        var filter = section.GetSection("Filter");

        return new JobProviderOptions
        {
            Adzuna = new AdzunaOptions
            {
                AppId = Text(adzuna, "AppId"),
                AppKey = Text(adzuna, "AppKey"),
                ResultsPerPage = Integer(adzuna, "ResultsPerPage", fallback: 50)
            },
            Jooble = new JoobleOptions
            {
                ApiKey = Text(jooble, "ApiKey")
            },
            Filter = new JobProviderFilterOptions
            {
                TitleKeywords = List(filter, "TitleKeywords"),
                LocationKeywords = List(filter, "LocationKeywords")
            }
        };
    }

    private static string[] List(IConfiguration section, string key)
        => section
            .GetSection(key)
            .GetChildren()
            .Select(child => child.Value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();

    private static string? Text(IConfiguration section, string key)
    {
        var value = section[key];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int Integer(IConfiguration section, string key, int fallback)
        => int.TryParse(section[key], out var value) ? value : fallback;

    private static bool Boolean(IConfiguration section, string key, bool fallback)
        => bool.TryParse(section[key], out var value) ? value : fallback;
}
