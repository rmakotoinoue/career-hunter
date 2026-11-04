using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Domain.Candidates;

public sealed class CandidateExperience : Entity
{
    private readonly List<string> _technologies = [];

    private CandidateExperience()
    {
        Employer = string.Empty;
        Title = string.Empty;
    }

    private CandidateExperience(
        Guid candidateProfileId,
        string employer,
        string title,
        DateOnly startDate,
        DateOnly? endDate,
        string? description,
        IEnumerable<string>? technologies,
        DateOnly today)
    {
        CandidateProfileId = candidateProfileId;
        Employer = Guard.NotBlank(employer, "employer", 160);
        Title = Guard.NotBlank(title, "title", 160);
        Description = Guard.Optional(description, "description", 4000);
        StartDate = startDate;
        EndDate = endDate;
        ValidateDates(startDate, endDate, today);

        if (endDate is not null && endDate.Value > today)
        {
            throw new DomainValidationException("endDate", "End date cannot be in the future.");
        }

        if (technologies is not null)
        {
            foreach (var technology in technologies)
            {
                var normalized = Guard.Optional(technology, "technologies", 60);
                if (normalized is not null
                    && !_technologies.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    _technologies.Add(normalized);
                }
            }
        }

        if (_technologies.Count > 30)
        {
            throw new DomainValidationException("technologies", "At most 30 technologies can be listed per experience.");
        }
    }

    public Guid CandidateProfileId { get; private set; }

    public string Employer { get; private set; }

    public string Title { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    /// <summary>
    /// A position without an end date is, by definition, the current position.
    /// The flag is derived instead of stored so it can never contradict the dates.
    /// </summary>
    public bool IsCurrent => EndDate is null;

    public string? Description { get; private set; }

    public IReadOnlyList<string> Technologies => _technologies;

    internal static CandidateExperience Create(
        Guid candidateProfileId,
        string employer,
        string title,
        DateOnly startDate,
        DateOnly? endDate,
        string? description,
        IEnumerable<string>? technologies,
        DateOnly today)
        => new(candidateProfileId, employer, title, startDate, endDate, description, technologies, today);

    public int DurationInMonths(DateOnly referenceDate)
    {
        var end = EndDate ?? referenceDate;
        if (end < StartDate)
        {
            return 0;
        }

        return ((end.Year - StartDate.Year) * 12) + end.Month - StartDate.Month;
    }

    private static void ValidateDates(DateOnly startDate, DateOnly? endDate, DateOnly today)
    {
        if (startDate == default)
        {
            throw new DomainValidationException("startDate", "Start date is required.");
        }

        if (startDate > today)
        {
            throw new DomainValidationException("startDate", "Start date cannot be in the future.");
        }

        if (endDate is not null && endDate.Value < startDate)
        {
            throw new DomainValidationException("endDate", "End date cannot be before the start date.");
        }
    }
}
