namespace CareerHunter.Domain.SharedKernel;

public enum SalaryPeriod
{
    Monthly = 1,
    Yearly = 2,
    Daily = 3,
    Hourly = 4
}

/// <summary>
/// Salary interval in a single currency. Used for both the candidate expectation and
/// the job offer so both sides are compared with identical semantics.
/// </summary>
public sealed class SalaryRange : IEquatable<SalaryRange>
{
    private SalaryRange()
    {
        Currency = "BRL";
    }

    private SalaryRange(decimal minAmount, decimal maxAmount, string currency, SalaryPeriod period)
    {
        MinAmount = minAmount;
        MaxAmount = maxAmount;
        Currency = currency;
        Period = period;
    }

    public decimal MinAmount { get; private set; }

    public decimal MaxAmount { get; private set; }

    public string Currency { get; private set; }

    public SalaryPeriod Period { get; private set; }

    public static SalaryRange Create(
        decimal minAmount,
        decimal maxAmount,
        SalaryPeriod period = SalaryPeriod.Monthly,
        string? currency = "BRL")
    {
        Guard.NotNegative(minAmount, "minAmount");
        Guard.NotNegative(maxAmount, "maxAmount");

        if (maxAmount < minAmount)
        {
            throw new DomainValidationException("maxAmount", "Maximum amount cannot be lower than the minimum amount.");
        }

        if (!Enum.IsDefined(period))
        {
            throw new DomainValidationException("period", "Unknown salary period.");
        }

        return new SalaryRange(
            decimal.Round(minAmount, 2, MidpointRounding.ToEven),
            decimal.Round(maxAmount, 2, MidpointRounding.ToEven),
            Guard.Currency(currency)!,
            period);
    }

    public bool Equals(SalaryRange? other)
        => other is not null
           && MinAmount == other.MinAmount
           && MaxAmount == other.MaxAmount
           && Currency == other.Currency
           && Period == other.Period;

    public override bool Equals(object? obj) => Equals(obj as SalaryRange);

    public override int GetHashCode() => HashCode.Combine(MinAmount, MaxAmount, Currency, Period);
}
