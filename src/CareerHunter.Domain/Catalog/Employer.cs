using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Domain.Catalog;

/// <summary>
/// Employer identity shared by many opportunities. It is its own aggregate so employer
/// deduplication (normalized name) has a single place to live.
/// </summary>
public sealed class Employer : AggregateRoot
{
    private Employer()
    {
        Name = string.Empty;
        NormalizedName = string.Empty;
    }

    private Employer(string name, string? website, DateTimeOffset now)
        : base(Guid.NewGuid(), now)
    {
        Name = Guard.NotBlank(name, "name", 200);
        NormalizedName = TextNormalizer.Company(Name);
        Website = website is null ? null : Guard.AbsoluteHttpUrl(website, "website");

        if (NormalizedName.Length < 2)
        {
            throw new DomainValidationException("name", "Employer name is too short to be identifiable.");
        }
    }

    public string Name { get; private set; }

    /// <summary>
    /// Legal-suffix-insensitive key ("Acme Ltda." and "ACME" collapse to "acme").
    /// </summary>
    public string NormalizedName { get; private set; }

    public string? Website { get; private set; }

    public static Employer Create(string name, string? website, DateTimeOffset now)
        => new(name, website, now);
}

/// <summary>
/// Location of an opportunity. City may be unknown for remote positions; unknown stays null.
/// </summary>
public sealed class JobLocation
{
    private JobLocation()
    {
        Country = string.Empty;
    }

    private JobLocation(string? city, string? state, string country)
    {
        City = city;
        State = state;
        Country = country;
    }

    public string? City { get; private set; }

    public string? State { get; private set; }

    public string Country { get; private set; }

    public static JobLocation Create(string? city, string? state, string? country = "Brazil")
        => new(
            Guard.Optional(city, "city", 120),
            Guard.Optional(state, "state", 60),
            Guard.NotBlank(country, "country", 60));

    public static JobLocation Unknown() => new(null, null, "Brazil");

    public override string ToString()
        => string.Join(", ", new[] { City, State, Country }.Where(part => !string.IsNullOrWhiteSpace(part)));
}

/// <summary>
/// Normalized compensation. Unknown salary is represented by null amounts, never by zero.
/// </summary>
public sealed class JobCompensation
{
    private JobCompensation()
    {
        Currency = "BRL";
    }

    private JobCompensation(decimal? minAmount, decimal? maxAmount, string currency, SalaryPeriod period, bool isEstimated)
    {
        MinAmount = minAmount;
        MaxAmount = maxAmount;
        Currency = currency;
        Period = period;
        IsEstimated = isEstimated;
    }

    public decimal? MinAmount { get; private set; }

    public decimal? MaxAmount { get; private set; }

    public string Currency { get; private set; }

    public SalaryPeriod Period { get; private set; }

    /// <summary>
    /// True when the values were inferred (for example from a range in the description)
    /// rather than explicitly published.
    /// </summary>
    public bool IsEstimated { get; private set; }

    public bool IsKnown => MinAmount is not null || MaxAmount is not null;

    public static JobCompensation? Create(
        decimal? minAmount,
        decimal? maxAmount,
        SalaryPeriod period = SalaryPeriod.Monthly,
        string? currency = "BRL",
        bool isEstimated = false)
    {
        if (minAmount is null && maxAmount is null)
        {
            return null;
        }

        if (minAmount is not null)
        {
            Guard.NotNegative(minAmount.Value, "minAmount");
        }

        if (maxAmount is not null)
        {
            Guard.NotNegative(maxAmount.Value, "maxAmount");
        }

        if (minAmount is not null && maxAmount is not null && maxAmount.Value < minAmount.Value)
        {
            throw new DomainValidationException("maxAmount", "Maximum compensation cannot be lower than the minimum.");
        }

        if (!Enum.IsDefined(period))
        {
            throw new DomainValidationException("period", "Unknown salary period.");
        }

        return new JobCompensation(minAmount, maxAmount, Guard.Currency(currency)!, period, isEstimated);
    }

    public SalaryRange ToRange()
    {
        if (MinAmount is null || MaxAmount is null)
        {
            throw new DomainException("A complete compensation range is required.");
        }

        return SalaryRange.Create(MinAmount.Value, MaxAmount.Value, Period, Currency);
    }
}
