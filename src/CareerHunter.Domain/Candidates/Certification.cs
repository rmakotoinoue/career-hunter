using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Domain.Candidates;

public sealed class Certification : Entity
{
    private Certification()
    {
        Name = string.Empty;
        Issuer = string.Empty;
    }

    private Certification(
        Guid candidateProfileId,
        string name,
        string issuer,
        DateOnly issuedOn,
        DateOnly? expiresOn,
        string? credentialUrl)
    {
        CandidateProfileId = candidateProfileId;
        Name = Guard.NotBlank(name, "name", 160);
        Issuer = Guard.NotBlank(issuer, "issuer", 160);
        IssuedOn = issuedOn;
        ExpiresOn = expiresOn;
        CredentialUrl = credentialUrl is null ? null : Guard.AbsoluteHttpUrl(credentialUrl, "credentialUrl");

        if (issuedOn == default)
        {
            throw new DomainValidationException("issuedOn", "Issue date is required.");
        }

        if (expiresOn is not null && expiresOn.Value < issuedOn)
        {
            throw new DomainValidationException("expiresOn", "Expiry date cannot be before the issue date.");
        }
    }

    public Guid CandidateProfileId { get; private set; }

    public string Name { get; private set; }

    public string Issuer { get; private set; }

    public DateOnly IssuedOn { get; private set; }

    public DateOnly? ExpiresOn { get; private set; }

    public string? CredentialUrl { get; private set; }

    public bool IsExpired(DateOnly today) => ExpiresOn is not null && ExpiresOn.Value < today;

    internal static Certification Create(
        Guid candidateProfileId,
        string name,
        string issuer,
        DateOnly issuedOn,
        DateOnly? expiresOn,
        string? credentialUrl)
        => new(candidateProfileId, name, issuer, issuedOn, expiresOn, credentialUrl);
}
