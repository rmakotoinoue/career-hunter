using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Domain.Candidates;

public sealed class Education : Entity
{
    private Education()
    {
        Institution = string.Empty;
        Degree = string.Empty;
    }

    private Education(
        Guid candidateProfileId,
        string institution,
        string degree,
        string? fieldOfStudy,
        DateOnly startDate,
        DateOnly? endDate,
        DateOnly today)
    {
        CandidateProfileId = candidateProfileId;
        Institution = Guard.NotBlank(institution, "institution", 160);
        Degree = Guard.NotBlank(degree, "degree", 160);
        FieldOfStudy = Guard.Optional(fieldOfStudy, "fieldOfStudy", 120);
        StartDate = startDate;
        EndDate = endDate;

        if (startDate == default || startDate > today)
        {
            throw new DomainValidationException("startDate", "Education start date is required and cannot be in the future.");
        }

        if (endDate is not null && endDate.Value < startDate)
        {
            throw new DomainValidationException("endDate", "Education end date cannot be before the start date.");
        }
    }

    public Guid CandidateProfileId { get; private set; }

    public string Institution { get; private set; }

    public string Degree { get; private set; }

    public string? FieldOfStudy { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public bool IsInProgress => EndDate is null;

    internal static Education Create(
        Guid candidateProfileId,
        string institution,
        string degree,
        string? fieldOfStudy,
        DateOnly startDate,
        DateOnly? endDate,
        DateOnly today)
        => new(candidateProfileId, institution, degree, fieldOfStudy, startDate, endDate, today);
}
