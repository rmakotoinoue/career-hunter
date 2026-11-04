namespace CareerHunter.Domain.SharedKernel;

/// <summary>
/// Monetary amount with an explicit ISO-4217 currency. The domain never guesses a currency.
/// </summary>
public sealed class Money : IEquatable<Money>
{
    private Money()
    {
        Currency = "BRL";
    }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; }

    public static Money Create(decimal amount, string? currency = "BRL")
    {
        Guard.NotNegative(amount, "amount");
        return new Money(decimal.Round(amount, 2, MidpointRounding.ToEven), Guard.Currency(currency)!);
    }

    public bool Equals(Money? other)
        => other is not null && Amount == other.Amount && Currency == other.Currency;

    public override bool Equals(object? obj) => Equals(obj as Money);

    public override int GetHashCode() => HashCode.Combine(Amount, Currency);

    public override string ToString() => $"{Currency} {Amount:0.00}";
}
