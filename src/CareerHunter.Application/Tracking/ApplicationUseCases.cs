using System.Net;
using System.Text;
using CareerHunter.Application.Abstractions;
using CareerHunter.Application.Common;
using CareerHunter.Domain.Assessment;
using CareerHunter.Domain.Candidates;
using CareerHunter.Domain.Catalog;
using CareerHunter.Domain.SharedKernel;
using CareerHunter.Domain.Tracking;

namespace CareerHunter.Application.Tracking;

public sealed record StartApplicationCommand(
    Guid CandidateProfileId,
    Guid JobOpportunityId,
    Guid? JobEvaluationId = null);

public sealed class StartApplicationUseCase(
    ICandidateProfileRepository candidates,
    IJobOpportunityRepository jobs,
    IEmployerRepository employers,
    IJobEvaluationRepository evaluations,
    IJobApplicationRepository applications,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Guid> ExecuteAsync(StartApplicationCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!await candidates.ExistsAsync(command.CandidateProfileId, cancellationToken))
        {
            throw new NotFoundException("CandidateProfile", command.CandidateProfileId);
        }

        var job = await jobs.GetAsync(command.JobOpportunityId, cancellationToken)
                  ?? throw new NotFoundException("JobOpportunity", command.JobOpportunityId);

        if (!job.IsActive)
        {
            throw new BusinessRuleException("An expired opportunity cannot be tracked as a new application.");
        }

        if (await applications.GetByJobAsync(command.CandidateProfileId, job.Id, cancellationToken) is not null)
        {
            throw new ConflictException("An application already exists for this opportunity and candidate.");
        }

        var employer = await employers.GetAsync(job.EmployerId, cancellationToken)
                       ?? throw new NotFoundException("Employer", job.EmployerId);

        if (command.JobEvaluationId is not null)
        {
            var evaluation = await evaluations.GetAsync(command.JobEvaluationId.Value, cancellationToken)
                             ?? throw new NotFoundException("JobEvaluation", command.JobEvaluationId.Value);

            if (evaluation.CandidateProfileId != command.CandidateProfileId || evaluation.JobOpportunityId != job.Id)
            {
                throw new BusinessRuleException("The evaluation does not belong to this candidate and opportunity.");
            }
        }

        var originalUrl = job.SourceReferences
            .OrderBy(reference => reference.PublishedAt ?? reference.CollectedAt)
            .Select(reference => reference.SourceUrl)
            .FirstOrDefault()
            ?? throw new BusinessRuleException("The opportunity has no original source URL and cannot be tracked.");

        var application = JobApplication.Start(
            command.CandidateProfileId,
            job.Id,
            job.Title,
            employer.Name,
            originalUrl,
            command.JobEvaluationId,
            clock.UtcNow);

        applications.Add(application);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return application.Id;
    }
}

public sealed record AddApplicationMaterialCommand(
    Guid ApplicationId,
    ApplicationMaterialType Type,
    string Title,
    string Content,
    string PreparedBy = "candidate",
    string? ModelName = null);

public sealed class AddApplicationMaterialUseCase(
    IJobApplicationRepository applications,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Guid> ExecuteAsync(AddApplicationMaterialCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var application = await applications.GetAsync(command.ApplicationId, cancellationToken)
                          ?? throw new NotFoundException("Application", command.ApplicationId);

        var material = application.AddMaterial(
            command.Type,
            command.Title,
            command.Content,
            command.PreparedBy,
            command.ModelName,
            clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return material.Id;
    }
}

public sealed record ChangeApplicationStatusCommand(
    Guid ApplicationId,
    ApplicationStatus TargetStatus,
    string ChangedBy,
    string? Reason = null,
    bool UserConfirmed = false);

public sealed class ChangeApplicationStatusUseCase(
    IJobApplicationRepository applications,
    IJobEvaluationRepository evaluations,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<ApplicationStatus> ExecuteAsync(
        ChangeApplicationStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var application = await applications.GetAsync(command.ApplicationId, cancellationToken)
                          ?? throw new NotFoundException("Application", command.ApplicationId);

        if (command.TargetStatus == ApplicationStatus.Analyzed)
        {
            var evaluation = application.JobEvaluationId is null
                ? null
                : await evaluations.GetAsync(application.JobEvaluationId.Value, cancellationToken);

            if (evaluation is null)
            {
                throw new BusinessRuleException("An evaluation must be linked before marking the application as Analyzed.");
            }
        }

        application.ChangeStatus(
            command.TargetStatus,
            command.ChangedBy,
            command.Reason,
            command.UserConfirmed,
            clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return application.Status;
    }
}

public sealed record AddApplicationNoteCommand(Guid ApplicationId, string Text, string Author = "candidate");

public sealed class AddApplicationNoteUseCase(
    IJobApplicationRepository applications,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Guid> ExecuteAsync(AddApplicationNoteCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var application = await applications.GetAsync(command.ApplicationId, cancellationToken)
                          ?? throw new NotFoundException("Application", command.ApplicationId);

        var note = application.AddNote(command.Text, command.Author, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return note.Id;
    }
}

public sealed record SetApplicationSalaryExpectationCommand(Guid ApplicationId, decimal Amount, string Currency = "BRL");

public sealed class SetApplicationSalaryExpectationUseCase(
    IJobApplicationRepository applications,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task ExecuteAsync(
        SetApplicationSalaryExpectationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var application = await applications.GetAsync(command.ApplicationId, cancellationToken)
                          ?? throw new NotFoundException("Application", command.ApplicationId);

        application.SetSalaryExpectation(Money.Create(command.Amount, command.Currency), clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed record PrepareApplicationPackageCommand(Guid ApplicationId);

/// <summary>
/// Prepares a resume summary and a recruiter message from data the candidate actually declared.
/// It never invents experience and never submits anything: the candidate reviews and applies manually.
/// </summary>
public sealed class PrepareApplicationPackageUseCase(
    IJobApplicationRepository applications,
    ICandidateProfileRepository candidates,
    IJobOpportunityRepository jobs,
    IEmployerRepository employers,
    IJobEvaluationRepository evaluations,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<IReadOnlyList<Guid>> ExecuteAsync(
        PrepareApplicationPackageCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var application = await applications.GetAsync(command.ApplicationId, cancellationToken)
                          ?? throw new NotFoundException("Application", command.ApplicationId);

        if (application.IsTerminal)
        {
            throw new BusinessRuleException("A closed application cannot receive new materials.");
        }

        var profile = await candidates.GetAsync(application.CandidateProfileId, cancellationToken)
                      ?? throw new NotFoundException("CandidateProfile", application.CandidateProfileId);

        var job = await jobs.GetAsync(application.JobOpportunityId, cancellationToken)
                  ?? throw new NotFoundException("JobOpportunity", application.JobOpportunityId);

        var employer = await employers.GetAsync(job.EmployerId, cancellationToken)
                       ?? throw new NotFoundException("Employer", job.EmployerId);

        JobEvaluation? evaluation = application.JobEvaluationId is null
            ? null
            : await evaluations.GetAsync(application.JobEvaluationId.Value, cancellationToken);

        var now = clock.UtcNow;
        var created = new List<Guid>();

        var resume = ApplicationMaterialComposer.BuildResumeSummary(profile, job, employer, evaluation);
        created.Add(application.AddMaterial(
            ApplicationMaterialType.Resume,
            $"Resumo direcionado — {job.Title}",
            resume,
            "rule-engine",
            null,
            now).Id);

        var message = ApplicationMaterialComposer.BuildRecruiterMessage(profile, job, employer, evaluation);
        created.Add(application.AddMaterial(
            ApplicationMaterialType.RecruiterMessage,
            $"Mensagem ao recrutador — {job.Title}",
            message,
            "rule-engine",
            null,
            now).Id);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return created;
    }
}

/// <summary>
/// Pure text composition from declared data. HTML is deliberately not produced here; the API
/// returns text and the user copies it into the application form.
/// </summary>
public static class ApplicationMaterialComposer
{
    public static string BuildResumeSummary(
        CandidateProfile profile,
        JobOpportunity job,
        Employer employer,
        JobEvaluation? evaluation)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Resumo profissional direcionado — {job.Title} @ {employer.Name}");
        builder.AppendLine();
        builder.AppendLine($"{profile.Headline}.");
        if (profile.YearsOfExperience is not null)
        {
            builder.AppendLine($"Experiência declarada: {profile.YearsOfExperience} anos.");
        }

        builder.AppendLine();

        var topSkills = profile.Skills
            .OrderByDescending(skill => skill.Proficiency)
            .ThenByDescending(skill => skill.YearsOfExperience)
            .Take(8)
            .Select(skill => $"{skill.Name} ({skill.YearsOfExperience:0.#} anos, proficiência {skill.Proficiency}/5)")
            .ToArray();

        builder.AppendLine("Competências declaradas mais relevantes:");
        foreach (var skill in topSkills)
        {
            builder.AppendLine($"- {skill}");
        }

        var matched = evaluation?.RequirementMatches
            .Where(match => match.Status is RequirementMatchStatus.Matched or RequirementMatchStatus.PartiallyMatched)
            .Select(match => match.RequirementName)
            .Take(10)
            .ToArray() ?? [];

        if (matched.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Requisitos da vaga com evidência no perfil:");
            foreach (var requirement in matched)
            {
                builder.AppendLine($"- {requirement}");
            }
        }

        var gaps = evaluation?.Gaps.Take(6).ToArray() ?? [];
        if (gaps.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Pontos a endereçar na conversa (não adicionados ao currículo):");
            foreach (var gap in gaps)
            {
                builder.AppendLine($"- {gap}");
            }
        }

        if (profile.Experiences.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Experiências declaradas (ordem cronológica reversa):");
            foreach (var experience in profile.Experiences
                         .OrderByDescending(experience => experience.EndDate ?? DateOnly.MaxValue)
                         .Take(5))
            {
                var period = experience.EndDate is null
                    ? $"{experience.StartDate:MM/yyyy} — atual"
                    : $"{experience.StartDate:MM/yyyy} — {experience.EndDate:MM/yyyy}";
                builder.AppendLine($"- {experience.Title} @ {experience.Employer} ({period})");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Material gerado automaticamente a partir do perfil declarado. Revisão manual obrigatória antes do envio.");
        return builder.ToString().Trim();
    }

    public static string BuildRecruiterMessage(
        CandidateProfile profile,
        JobOpportunity job,
        Employer employer,
        JobEvaluation? evaluation)
    {
        var highlighted = profile.Skills
            .OrderByDescending(skill => skill.Proficiency)
            .Take(3)
            .Select(skill => skill.Name)
            .ToArray();

        var matched = evaluation?.RequirementMatches
            .Where(match => match.Status == RequirementMatchStatus.Matched)
            .Select(match => match.RequirementName)
            .Take(3)
            .ToArray() ?? [];

        var builder = new StringBuilder();
        builder.AppendLine($"Olá, tudo bem?");
        builder.AppendLine();
        builder.AppendLine(
            $"Vi a oportunidade de {job.Title} na {employer.Name} e gostaria de me candidatar. " +
            $"Atuo como {profile.Headline.ToLowerInvariant()}");

        if (highlighted.Length > 0)
        {
            builder.Append($" com experiência em {string.Join(", ", highlighted)}");
        }

        builder.AppendLine(".");

        if (matched.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine(
                $"A vaga menciona {string.Join(", ", matched)}, temas que já fazem parte do meu histórico profissional.");
        }

        builder.AppendLine();
        builder.AppendLine("Posso enviar mais detalhes e meu currículo atualizado. Obrigado pela atenção!");
        builder.AppendLine(profile.FullName);
        builder.AppendLine();
        builder.AppendLine("Mensagem gerada para revisão manual. Nenhum envio automático foi realizado.");
        return builder.ToString().Trim();
    }

    public static string EncodeForHtml(string value) => WebUtility.HtmlEncode(value);
}
