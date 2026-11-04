using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerHunter.Infrastructure.Persistence;

/// <summary>
/// Provider-agnostic mapping for small string lists (technologies, strengths, gaps).
/// Stored as JSON text so the model works identically on PostgreSQL and SQLite (tests).
/// </summary>
internal static class StringListMapping
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static PropertyBuilder<IReadOnlyList<string>> HasJsonListConversion(
        this PropertyBuilder<IReadOnlyList<string>> builder,
        string columnName,
        int maxLength = 4000)
        => builder
            .HasColumnName(columnName)
            .HasMaxLength(maxLength)
            .HasConversion(
                value => JsonSerializer.Serialize(value, Options),
                value => Deserialize(value),
                new ValueComparer<IReadOnlyList<string>>(
                    (left, right) => left != null && right != null && left.SequenceEqual(right),
                    value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode(StringComparison.Ordinal))),
                    // The model type is IReadOnlyList<string>, but EF assigns the value to the
                    // domain's List<string> backing field, so the concrete type must stay a list.
                    value => value.ToList()));

    private static List<string> Deserialize(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : JsonSerializer.Deserialize<string[]>(value, Options)?.ToList() ?? [];
}

internal static class OwnedMoneyMapping
{
    public static void MapMoney<T>(OwnedNavigationBuilder<T, Domain.SharedKernel.Money> builder, string prefix)
        where T : class
    {
        builder.Property(money => money.Amount)
            .HasColumnName($"{prefix}_amount")
            .HasPrecision(18, 2);

        builder.Property(money => money.Currency)
            .HasColumnName($"{prefix}_currency")
            .HasMaxLength(3)
            .IsRequired();
    }

    public static void MapSalaryRange<T>(
        OwnedNavigationBuilder<T, Domain.SharedKernel.SalaryRange> builder,
        string prefix)
        where T : class
    {
        builder.Property(range => range.MinAmount)
            .HasColumnName($"{prefix}_min")
            .HasPrecision(18, 2);

        builder.Property(range => range.MaxAmount)
            .HasColumnName($"{prefix}_max")
            .HasPrecision(18, 2);

        builder.Property(range => range.Currency)
            .HasColumnName($"{prefix}_currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(range => range.Period)
            .HasColumnName($"{prefix}_period")
            .HasConversion<string>()
            .HasMaxLength(20);
    }
}
