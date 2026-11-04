using CareerHunter.Application.Abstractions;
using CareerHunter.Application.Assessment;
using CareerHunter.Application.Candidates;
using CareerHunter.Application.Catalog;
using CareerHunter.Application.Discovery;
using CareerHunter.Application.Notifications;
using CareerHunter.Application.Tracking;
using Microsoft.Extensions.DependencyInjection;

namespace CareerHunter.Application;

/// <summary>
/// Registers use cases and pure domain services. Infrastructure is deliberately not referenced:
/// the composition root supplies the ports.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        AddCandidateUseCases(services);
        AddDiscoveryUseCases(services);
        AddCatalogServices(services);
        AddAssessmentServices(services);
        AddTrackingUseCases(services);
        AddNotificationUseCases(services);

        return services;
    }

    private static void AddCandidateUseCases(IServiceCollection services)
    {
        services.AddScoped<CreateCandidateProfileUseCase>();
        services.AddScoped<UpdateCandidateProfileUseCase>();
        services.AddScoped<UpdateWorkPreferencesUseCase>();
        services.AddScoped<AddCandidateExperienceUseCase>();
        services.AddScoped<AddCandidateSkillUseCase>();
        services.AddScoped<RemoveCandidateSkillUseCase>();
        services.AddScoped<AddCandidateEducationUseCase>();
        services.AddScoped<AddCandidateCertificationUseCase>();
        services.AddScoped<AddCareerTargetUseCase>();
        services.AddScoped<SetPrimaryCareerTargetUseCase>();
    }

    private static void AddDiscoveryUseCases(IServiceCollection services)
    {
        services.AddScoped<RegisterJobSourceUseCase>();
        services.AddScoped<SetJobSourceEnabledUseCase>();
        services.AddScoped<SubmitManualJobRecordUseCase>();
        services.AddScoped<CollectJobsUseCase>();
        services.AddScoped<NormalizeSourceRecordsUseCase>();
    }

    private static void AddCatalogServices(IServiceCollection services)
    {
        services.AddSingleton<JobRequirementExtractor>();
        services.AddSingleton<JobNormalizer>();
    }

    private static void AddAssessmentServices(IServiceCollection services)
    {
        services.AddSingleton<EvaluationSnapshotFactory>();
        services.AddSingleton<EvaluationPromptBuilder>();
        services.AddSingleton<JobEvaluationResultValidator>();
        services.AddSingleton<RuleBasedEvaluationEngine>();
        services.AddScoped<AiEvaluationEngine>();
        services.AddScoped<EvaluateJobOpportunityUseCase>();
    }

    private static void AddTrackingUseCases(IServiceCollection services)
    {
        services.AddScoped<StartApplicationUseCase>();
        services.AddScoped<AddApplicationMaterialUseCase>();
        services.AddScoped<ChangeApplicationStatusUseCase>();
        services.AddScoped<AddApplicationNoteUseCase>();
        services.AddScoped<SetApplicationSalaryExpectationUseCase>();
        services.AddScoped<PrepareApplicationPackageUseCase>();
    }

    private static void AddNotificationUseCases(IServiceCollection services)
    {
        services.AddScoped<ConfigureNotificationPreferenceUseCase>();
        services.AddScoped<GenerateDailyDigestUseCase>();
        services.AddScoped<SendDailyDigestUseCase>();
        services.AddScoped<PreviewDailyDigestUseCase>();
    }
}
