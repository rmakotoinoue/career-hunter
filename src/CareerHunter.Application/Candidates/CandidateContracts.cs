using CareerHunter.Domain.Candidates;
using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Application.Candidates;

public sealed record CandidateLocationDto(string City, string? State, string Country);

public sealed record WorkPreferencesDto(
    bool AcceptsRemote,
    bool AcceptsHybrid,
    bool AcceptsOnsite,
    bool AcceptsClt,
    bool AcceptsPj);

public sealed record SalaryRangeDto(decimal MinAmount, decimal MaxAmount, string Currency, SalaryPeriod Period);

public sealed record CandidateExperienceDto(
    Guid Id,
    string Employer,
    string Title,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsCurrent,
    string? Description,
    IReadOnlyList<string> Technologies);

public sealed record CandidateSkillDto(
    Guid Id,
    string Name,
    SkillCategory Category,
    int Proficiency,
    decimal YearsOfExperience);

public sealed record EducationDto(
    Guid Id,
    string Institution,
    string Degree,
    string? FieldOfStudy,
    DateOnly StartDate,
    DateOnly? EndDate);

public sealed record CertificationDto(
    Guid Id,
    string Name,
    string Issuer,
    DateOnly IssuedOn,
    DateOnly? ExpiresOn,
    string? CredentialUrl);

public sealed record CareerTargetDto(
    Guid Id,
    string JobTitle,
    SeniorityLevel Seniority,
    bool IsPrimary,
    string? Notes);

public sealed record CandidateProfileDto(
    Guid Id,
    string FullName,
    string Email,
    string Headline,
    string? Summary,
    string? LinkedInUrl,
    int? YearsOfExperience,
    CandidateLocationDto Location,
    SalaryRangeDto? CompensationExpectation,
    WorkPreferencesDto? WorkPreferences,
    IReadOnlyList<CandidateExperienceDto> Experiences,
    IReadOnlyList<CandidateSkillDto> Skills,
    IReadOnlyList<EducationDto> Educations,
    IReadOnlyList<CertificationDto> Certifications,
    IReadOnlyList<CareerTargetDto> CareerTargets,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CandidateProfileSummaryDto(
    Guid Id,
    string FullName,
    string Email,
    string Headline,
    string? PrimaryCareerTarget,
    int SkillCount,
    int ExperienceCount,
    DateTimeOffset UpdatedAt);
