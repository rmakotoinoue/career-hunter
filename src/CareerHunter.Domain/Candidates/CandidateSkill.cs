using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Domain.Candidates;

public enum SkillCategory
{
    Technical = 1,
    Behavioral = 2,
    Language = 3,
    Tool = 4,
    Domain = 5
}

/// <summary>
/// A skill declared by the candidate. Skills are never inferred from job requirements:
/// only the candidate can add them.
/// </summary>
public sealed class CandidateSkill : Entity
{
    private CandidateSkill()
    {
        Name = string.Empty;
        NormalizedName = string.Empty;
    }

    private CandidateSkill(
        Guid candidateProfileId,
        string name,
        SkillCategory category,
        int proficiency,
        decimal yearsOfExperience)
    {
        CandidateProfileId = candidateProfileId;
        Name = Guard.NotBlank(name, "name", 80);
        NormalizedName = TextNormalizer.ForMatching(Name);
        Category = category;
        Proficiency = Guard.InRange(proficiency, "proficiency", 1, 5);
        YearsOfExperience = decimal.Round(
            Guard.NotNegative(yearsOfExperience, "yearsOfExperience"),
            1,
            MidpointRounding.ToEven);

        if (YearsOfExperience > 60)
        {
            throw new DomainValidationException("yearsOfExperience", "Years of experience cannot exceed 60.");
        }

        if (!Enum.IsDefined(category))
        {
            throw new DomainValidationException("category", "Unknown skill category.");
        }
    }

    public Guid CandidateProfileId { get; private set; }

    public string Name { get; private set; }

    /// <summary>
    /// Accent/case-insensitive key used to keep the profile free of duplicate skills.
    /// </summary>
    public string NormalizedName { get; private set; }

    public SkillCategory Category { get; private set; }

    /// <summary>
    /// Self-declared proficiency from 1 (basic) to 5 (expert).
    /// </summary>
    public int Proficiency { get; private set; }

    public decimal YearsOfExperience { get; private set; }

    internal static CandidateSkill Create(
        Guid candidateProfileId,
        string name,
        SkillCategory category,
        int proficiency,
        decimal yearsOfExperience)
        => new(candidateProfileId, name, category, proficiency, yearsOfExperience);

    internal void Update(SkillCategory category, int proficiency, decimal yearsOfExperience)
    {
        Category = category;
        Proficiency = Guard.InRange(proficiency, "proficiency", 1, 5);
        YearsOfExperience = decimal.Round(
            Guard.NotNegative(yearsOfExperience, "yearsOfExperience"),
            1,
            MidpointRounding.ToEven);
    }
}
