using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Domain.Candidates;

/// <summary>
/// Aggregate root of the CandidateProfile context.
/// It owns the candidate's history, skills, education, certifications, career targets,
/// work preferences and compensation expectation. Other contexts reference it by id only.
/// </summary>
public sealed class CandidateProfile : AggregateRoot
{
    private readonly List<CandidateExperience> _experiences = [];
    private readonly List<CandidateSkill> _skills = [];
    private readonly List<Education> _educations = [];
    private readonly List<Certification> _certifications = [];
    private readonly List<CareerTarget> _careerTargets = [];

    private CandidateProfile()
    {
        FullName = string.Empty;
        Headline = string.Empty;
        Location = null!;
    }

    private CandidateProfile(
        Guid id,
        string fullName,
        EmailAddress email,
        string headline,
        string? summary,
        CandidateLocation location,
        int? yearsOfExperience,
        string? linkedInUrl,
        DateTimeOffset now)
        : base(id, now)
    {
        FullName = Guard.NotBlank(fullName, "fullName", 120, 2);
        Email = email ?? throw new DomainValidationException("email", "E-mail is required.");
        Headline = Guard.NotBlank(headline, "headline", 160, 2);
        Summary = Guard.Optional(summary, "summary", 4000);
        Location = location ?? throw new DomainValidationException("location", "Location is required.");
        YearsOfExperience = ValidateYears(yearsOfExperience);
        LinkedInUrl = linkedInUrl is null ? null : Guard.AbsoluteHttpUrl(linkedInUrl, "linkedInUrl");
    }

    public string FullName { get; private set; }

    public EmailAddress Email { get; private set; } = null!;

    public string Headline { get; private set; }

    public string? Summary { get; private set; }

    public string? LinkedInUrl { get; private set; }

    public int? YearsOfExperience { get; private set; }

    public CandidateLocation Location { get; private set; }

    /// <summary>
    /// Null until the candidate declares an expectation. Never defaulted.
    /// </summary>
    public SalaryRange? CompensationExpectation { get; private set; }

    /// <summary>
    /// Null until the candidate declares accepted arrangements.
    /// </summary>
    public WorkPreferences? WorkPreferences { get; private set; }

    public IReadOnlyList<CandidateExperience> Experiences => _experiences;

    public IReadOnlyList<CandidateSkill> Skills => _skills;

    public IReadOnlyList<Education> Educations => _educations;

    public IReadOnlyList<Certification> Certifications => _certifications;

    public IReadOnlyList<CareerTarget> CareerTargets => _careerTargets;

    public CareerTarget? PrimaryCareerTarget => _careerTargets.FirstOrDefault(target => target.IsPrimary);

    public static CandidateProfile Create(
        string fullName,
        EmailAddress email,
        string headline,
        string? summary,
        CandidateLocation location,
        int? yearsOfExperience,
        string? linkedInUrl,
        DateTimeOffset now)
        => new(Guid.NewGuid(), fullName, email, headline, summary, location, yearsOfExperience, linkedInUrl, now);

    public void UpdateBasicInfo(
        string fullName,
        string headline,
        string? summary,
        int? yearsOfExperience,
        string? linkedInUrl,
        DateTimeOffset now)
    {
        FullName = Guard.NotBlank(fullName, "fullName", 120, 2);
        Headline = Guard.NotBlank(headline, "headline", 160, 2);
        Summary = Guard.Optional(summary, "summary", 4000);
        YearsOfExperience = ValidateYears(yearsOfExperience);
        LinkedInUrl = linkedInUrl is null ? null : Guard.AbsoluteHttpUrl(linkedInUrl, "linkedInUrl");
        Touch(now);
    }

    public void UpdateLocation(CandidateLocation location, DateTimeOffset now)
    {
        Location = location ?? throw new DomainValidationException("location", "Location is required.");
        Touch(now);
    }

    public void UpdateWorkPreferences(WorkPreferences preferences, DateTimeOffset now)
    {
        WorkPreferences = preferences ?? throw new DomainValidationException("workPreferences", "Work preferences are required.");
        Touch(now);
    }

    public void UpdateCompensationExpectation(SalaryRange? expectation, DateTimeOffset now)
    {
        CompensationExpectation = expectation;
        Touch(now);
    }

    public CandidateExperience AddExperience(
        string employer,
        string title,
        DateOnly startDate,
        DateOnly? endDate,
        string? description,
        IEnumerable<string>? technologies,
        DateTimeOffset now)
    {
        var experience = CandidateExperience.Create(
            Id,
            employer,
            title,
            startDate,
            endDate,
            description,
            technologies,
            DateOnly.FromDateTime(now.UtcDateTime));

        _experiences.Add(experience);
        Touch(now);
        return experience;
    }

    public CandidateSkill AddSkill(
        string name,
        SkillCategory category,
        int proficiency,
        decimal yearsOfExperience,
        DateTimeOffset now)
    {
        var skill = CandidateSkill.Create(Id, name, category, proficiency, yearsOfExperience);
        if (_skills.Any(existing => existing.NormalizedName == skill.NormalizedName))
        {
            throw new DomainException($"Skill '{skill.Name}' is already part of the profile.");
        }

        _skills.Add(skill);
        Touch(now);
        return skill;
    }

    public void RemoveSkill(Guid skillId, DateTimeOffset now)
    {
        var skill = _skills.FirstOrDefault(existing => existing.Id == skillId)
            ?? throw new DomainException("Skill not found in the profile.");

        _skills.Remove(skill);
        Touch(now);
    }

    public Education AddEducation(
        string institution,
        string degree,
        string? fieldOfStudy,
        DateOnly startDate,
        DateOnly? endDate,
        DateTimeOffset now)
    {
        var education = Education.Create(
            Id,
            institution,
            degree,
            fieldOfStudy,
            startDate,
            endDate,
            DateOnly.FromDateTime(now.UtcDateTime));

        _educations.Add(education);
        Touch(now);
        return education;
    }

    public Certification AddCertification(
        string name,
        string issuer,
        DateOnly issuedOn,
        DateOnly? expiresOn,
        string? credentialUrl,
        DateTimeOffset now)
    {
        var certification = Certification.Create(Id, name, issuer, issuedOn, expiresOn, credentialUrl);
        _certifications.Add(certification);
        Touch(now);
        return certification;
    }

    public CareerTarget AddCareerTarget(
        string jobTitle,
        SeniorityLevel seniority,
        bool isPrimary,
        string? notes,
        DateTimeOffset now)
    {
        var target = CareerTarget.Create(Id, jobTitle, seniority, isPrimary, notes);
        if (_careerTargets.Any(existing => existing.NormalizedJobTitle == target.NormalizedJobTitle))
        {
            throw new DomainException($"Career target '{target.JobTitle}' is already registered.");
        }

        if (isPrimary)
        {
            DemotePrimaryTargets();
        }

        _careerTargets.Add(target);
        Touch(now);
        return target;
    }

    public void SetPrimaryCareerTarget(Guid careerTargetId, DateTimeOffset now)
    {
        var target = _careerTargets.FirstOrDefault(existing => existing.Id == careerTargetId)
            ?? throw new DomainException("Career target not found in the profile.");

        DemotePrimaryTargets();
        target.MakePrimary();
        Touch(now);
    }

    private void DemotePrimaryTargets()
    {
        foreach (var target in _careerTargets.Where(existing => existing.IsPrimary))
        {
            target.MakeSecondary();
        }
    }

    private static int? ValidateYears(int? years)
    {
        if (years is null)
        {
            return null;
        }

        return Guard.InRange(years.Value, "yearsOfExperience", 0, 60);
    }
}
