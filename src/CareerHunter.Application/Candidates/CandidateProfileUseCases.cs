using CareerHunter.Application.Abstractions;
using CareerHunter.Application.Common;
using CareerHunter.Domain.Candidates;
using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Application.Candidates;

public sealed record CreateCandidateProfileCommand(
    string FullName,
    string Email,
    string Headline,
    string? Summary,
    string? City,
    string? State,
    string? Country,
    int? YearsOfExperience,
    string? LinkedInUrl,
    bool AcceptsRemote,
    bool AcceptsHybrid,
    bool AcceptsOnsite,
    bool AcceptsClt,
    bool AcceptsPj,
    decimal? ExpectedMinAmount,
    decimal? ExpectedMaxAmount,
    SalaryPeriod ExpectedPeriod = SalaryPeriod.Monthly,
    string? ExpectedCurrency = "BRL");

public sealed class CreateCandidateProfileUseCase(
    ICandidateProfileRepository repository,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Guid> ExecuteAsync(CreateCandidateProfileCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var profile = CandidateProfile.Create(
            command.FullName,
            EmailAddress.Create(command.Email),
            command.Headline,
            command.Summary,
            CandidateLocation.Create(command.City, command.State, command.Country),
            command.YearsOfExperience,
            command.LinkedInUrl,
            clock.UtcNow);

        var hasAnyPreference = command.AcceptsRemote || command.AcceptsHybrid || command.AcceptsOnsite
                               || command.AcceptsClt || command.AcceptsPj;

        if (hasAnyPreference)
        {
            profile.UpdateWorkPreferences(
                WorkPreferences.Create(
                    command.AcceptsRemote,
                    command.AcceptsHybrid,
                    command.AcceptsOnsite,
                    command.AcceptsClt,
                    command.AcceptsPj),
                clock.UtcNow);
        }

        if (command.ExpectedMinAmount is not null || command.ExpectedMaxAmount is not null)
        {
            profile.UpdateCompensationExpectation(
                BuildExpectation(command),
                clock.UtcNow);
        }

        repository.Add(profile);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return profile.Id;
    }

    internal static SalaryRange BuildExpectation(CreateCandidateProfileCommand command)
    {
        var min = command.ExpectedMinAmount
                  ?? throw new DomainValidationException("expectedMinAmount", "Minimum expectation is required when a maximum is informed.");
        var max = command.ExpectedMaxAmount ?? min;
        return SalaryRange.Create(min, max, command.ExpectedPeriod, command.ExpectedCurrency);
    }
}

public sealed record UpdateCandidateProfileCommand(
    Guid Id,
    string FullName,
    string Headline,
    string? Summary,
    string? City,
    string? State,
    string? Country,
    int? YearsOfExperience,
    string? LinkedInUrl);

public sealed class UpdateCandidateProfileUseCase(
    ICandidateProfileRepository repository,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task ExecuteAsync(UpdateCandidateProfileCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var profile = await repository.GetAsync(command.Id, cancellationToken)
                      ?? throw new NotFoundException("CandidateProfile", command.Id);

        profile.UpdateBasicInfo(
            command.FullName,
            command.Headline,
            command.Summary,
            command.YearsOfExperience,
            command.LinkedInUrl,
            clock.UtcNow);

        profile.UpdateLocation(
            CandidateLocation.Create(command.City, command.State, command.Country),
            clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed record UpdateWorkPreferencesCommand(
    Guid CandidateProfileId,
    bool AcceptsRemote,
    bool AcceptsHybrid,
    bool AcceptsOnsite,
    bool AcceptsClt,
    bool AcceptsPj,
    decimal? ExpectedMinAmount,
    decimal? ExpectedMaxAmount,
    SalaryPeriod ExpectedPeriod = SalaryPeriod.Monthly,
    string? ExpectedCurrency = "BRL",
    bool ClearCompensationExpectation = false);

public sealed class UpdateWorkPreferencesUseCase(
    ICandidateProfileRepository repository,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task ExecuteAsync(UpdateWorkPreferencesCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var profile = await repository.GetAsync(command.CandidateProfileId, cancellationToken)
                      ?? throw new NotFoundException("CandidateProfile", command.CandidateProfileId);

        profile.UpdateWorkPreferences(
            WorkPreferences.Create(
                command.AcceptsRemote,
                command.AcceptsHybrid,
                command.AcceptsOnsite,
                command.AcceptsClt,
                command.AcceptsPj),
            clock.UtcNow);

        if (command.ClearCompensationExpectation)
        {
            profile.UpdateCompensationExpectation(null, clock.UtcNow);
        }
        else if (command.ExpectedMinAmount is not null || command.ExpectedMaxAmount is not null)
        {
            var min = command.ExpectedMinAmount
                      ?? throw new DomainValidationException("expectedMinAmount", "Minimum expectation is required when a maximum is informed.");
            profile.UpdateCompensationExpectation(
                SalaryRange.Create(min, command.ExpectedMaxAmount ?? min, command.ExpectedPeriod, command.ExpectedCurrency),
                clock.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed record AddCandidateExperienceCommand(
    Guid CandidateProfileId,
    string Employer,
    string Title,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Description,
    IReadOnlyList<string>? Technologies);

public sealed class AddCandidateExperienceUseCase(
    ICandidateProfileRepository repository,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Guid> ExecuteAsync(AddCandidateExperienceCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var profile = await repository.GetAsync(command.CandidateProfileId, cancellationToken)
                      ?? throw new NotFoundException("CandidateProfile", command.CandidateProfileId);

        var experience = profile.AddExperience(
            command.Employer,
            command.Title,
            command.StartDate,
            command.EndDate,
            command.Description,
            command.Technologies,
            clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return experience.Id;
    }
}

public sealed record AddCandidateSkillCommand(
    Guid CandidateProfileId,
    string Name,
    SkillCategory Category,
    int Proficiency,
    decimal YearsOfExperience);

public sealed class AddCandidateSkillUseCase(
    ICandidateProfileRepository repository,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Guid> ExecuteAsync(AddCandidateSkillCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var profile = await repository.GetAsync(command.CandidateProfileId, cancellationToken)
                      ?? throw new NotFoundException("CandidateProfile", command.CandidateProfileId);

        var skill = profile.AddSkill(
            command.Name,
            command.Category,
            command.Proficiency,
            command.YearsOfExperience,
            clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return skill.Id;
    }
}

public sealed record RemoveCandidateSkillCommand(Guid CandidateProfileId, Guid SkillId);

public sealed class RemoveCandidateSkillUseCase(
    ICandidateProfileRepository repository,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task ExecuteAsync(RemoveCandidateSkillCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var profile = await repository.GetAsync(command.CandidateProfileId, cancellationToken)
                      ?? throw new NotFoundException("CandidateProfile", command.CandidateProfileId);

        profile.RemoveSkill(command.SkillId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed record AddCandidateEducationCommand(
    Guid CandidateProfileId,
    string Institution,
    string Degree,
    string? FieldOfStudy,
    DateOnly StartDate,
    DateOnly? EndDate);

public sealed class AddCandidateEducationUseCase(
    ICandidateProfileRepository repository,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Guid> ExecuteAsync(AddCandidateEducationCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var profile = await repository.GetAsync(command.CandidateProfileId, cancellationToken)
                      ?? throw new NotFoundException("CandidateProfile", command.CandidateProfileId);

        var education = profile.AddEducation(
            command.Institution,
            command.Degree,
            command.FieldOfStudy,
            command.StartDate,
            command.EndDate,
            clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return education.Id;
    }
}

public sealed record AddCandidateCertificationCommand(
    Guid CandidateProfileId,
    string Name,
    string Issuer,
    DateOnly IssuedOn,
    DateOnly? ExpiresOn,
    string? CredentialUrl);

public sealed class AddCandidateCertificationUseCase(
    ICandidateProfileRepository repository,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Guid> ExecuteAsync(AddCandidateCertificationCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var profile = await repository.GetAsync(command.CandidateProfileId, cancellationToken)
                      ?? throw new NotFoundException("CandidateProfile", command.CandidateProfileId);

        var certification = profile.AddCertification(
            command.Name,
            command.Issuer,
            command.IssuedOn,
            command.ExpiresOn,
            command.CredentialUrl,
            clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return certification.Id;
    }
}

public sealed record AddCareerTargetCommand(
    Guid CandidateProfileId,
    string JobTitle,
    SeniorityLevel Seniority,
    bool IsPrimary,
    string? Notes);

public sealed class AddCareerTargetUseCase(
    ICandidateProfileRepository repository,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Guid> ExecuteAsync(AddCareerTargetCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var profile = await repository.GetAsync(command.CandidateProfileId, cancellationToken)
                      ?? throw new NotFoundException("CandidateProfile", command.CandidateProfileId);

        var target = profile.AddCareerTarget(
            command.JobTitle,
            command.Seniority,
            command.IsPrimary,
            command.Notes,
            clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return target.Id;
    }
}

public sealed record SetPrimaryCareerTargetCommand(Guid CandidateProfileId, Guid CareerTargetId);

public sealed class SetPrimaryCareerTargetUseCase(
    ICandidateProfileRepository repository,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task ExecuteAsync(SetPrimaryCareerTargetCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var profile = await repository.GetAsync(command.CandidateProfileId, cancellationToken)
                      ?? throw new NotFoundException("CandidateProfile", command.CandidateProfileId);

        profile.SetPrimaryCareerTarget(command.CareerTargetId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
