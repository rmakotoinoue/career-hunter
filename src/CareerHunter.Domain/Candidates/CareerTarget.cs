using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Domain.Candidates;

/// <summary>
/// A desired position. A profile may hold several targets (for example .NET Architect
/// and Tech Lead) but at most one is primary at any time.
/// </summary>
public sealed class CareerTarget : Entity
{
    private CareerTarget()
    {
        JobTitle = string.Empty;
        NormalizedJobTitle = string.Empty;
    }

    private CareerTarget(
        Guid candidateProfileId,
        string jobTitle,
        SeniorityLevel seniority,
        bool isPrimary,
        string? notes)
    {
        CandidateProfileId = candidateProfileId;
        JobTitle = Guard.NotBlank(jobTitle, "jobTitle", 160);
        NormalizedJobTitle = TextNormalizer.ForMatching(JobTitle);
        Seniority = seniority;
        IsPrimary = isPrimary;
        Notes = Guard.Optional(notes, "notes", 1000);

        if (!Enum.IsDefined(seniority))
        {
            throw new DomainValidationException("seniority", "Unknown seniority level.");
        }
    }

    public Guid CandidateProfileId { get; private set; }

    public string JobTitle { get; private set; }

    public string NormalizedJobTitle { get; private set; }

    public SeniorityLevel Seniority { get; private set; }

    public bool IsPrimary { get; private set; }

    public string? Notes { get; private set; }

    internal static CareerTarget Create(
        Guid candidateProfileId,
        string jobTitle,
        SeniorityLevel seniority,
        bool isPrimary,
        string? notes)
        => new(candidateProfileId, jobTitle, seniority, isPrimary, notes);

    internal void MakePrimary() => IsPrimary = true;

    internal void MakeSecondary() => IsPrimary = false;
}

/// <summary>
/// Location declared by the candidate.
/// </summary>
public sealed class CandidateLocation
{
    private CandidateLocation()
    {
        City = string.Empty;
        Country = string.Empty;
    }

    private CandidateLocation(string city, string? state, string country)
    {
        City = city;
        State = state;
        Country = country;
    }

    public string City { get; private set; }

    public string? State { get; private set; }

    public string Country { get; private set; }

    public static CandidateLocation Create(string? city, string? state, string? country = "Brazil")
    {
        var normalizedState = Guard.Optional(state, "state", 60);
        return new CandidateLocation(
            Guard.NotBlank(city, "city", 120),
            normalizedState,
            Guard.NotBlank(country, "country", 60));
    }
}
