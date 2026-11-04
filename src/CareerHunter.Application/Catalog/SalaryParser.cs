using System.Globalization;
using System.Text.RegularExpressions;

namespace CareerHunter.Application.Catalog;

/// <summary>
/// Conservative salary extraction. Only explicit monetary values published in the job text
/// are used; "a combinar" and absent values stay unknown (null), never zero.
/// </summary>
public static partial class SalaryParser
{
    public const decimal MonthlyBusinessDays = 21m;
    public const decimal MonthlyHours = 168m;

    public sealed record ParsedSalary(decimal MinAmount, decimal MaxAmount, string Currency, Domain.SharedKernel.SalaryPeriod Period, bool IsEstimated);

    public static ParsedSalary? TryParse(string? text, out string? reason)
    {
        reason = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var normalized = text.Replace('\u00A0', ' ');

        var range = RangePattern().Match(normalized);
        if (range.Success)
        {
            var first = ParseAmount(range.Groups["first"].Value);
            var second = ParseAmount(range.Groups["second"].Value);
            if (first is null || second is null)
            {
                reason = "Salary range could not be parsed.";
                return null;
            }

            if (second < first)
            {
                reason = "Salary range has an inverted interval.";
                return null;
            }

            var period = DetectPeriod(normalized, out var explicitPeriod);
            return new ParsedSalary(first.Value, second.Value, "BRL", period, !explicitPeriod);
        }

        var single = SinglePattern().Match(normalized);
        if (!single.Success)
        {
            return null;
        }

        var amount = ParseAmount(single.Groups["value"].Value);
        if (amount is null)
        {
            reason = "Salary value could not be parsed.";
            return null;
        }

        var singlePeriod = DetectPeriod(normalized, out var explicitSingle);
        return new ParsedSalary(amount.Value, amount.Value, "BRL", singlePeriod, !explicitSingle);
    }

    /// <summary>
    /// Converts a known amount to a monthly basis for comparison purposes.
    /// </summary>
    public static decimal ToMonthly(decimal amount, Domain.SharedKernel.SalaryPeriod period) => period switch
    {
        Domain.SharedKernel.SalaryPeriod.Monthly => amount,
        Domain.SharedKernel.SalaryPeriod.Yearly => amount / 12m,
        Domain.SharedKernel.SalaryPeriod.Daily => amount * MonthlyBusinessDays,
        Domain.SharedKernel.SalaryPeriod.Hourly => amount * MonthlyHours,
        _ => amount
    };

    private static Domain.SharedKernel.SalaryPeriod DetectPeriod(string text, out bool explicitPeriod)
    {
        var lower = text.ToLowerInvariant();

        if (YearlyHints().IsMatch(lower))
        {
            explicitPeriod = true;
            return Domain.SharedKernel.SalaryPeriod.Yearly;
        }

        if (HourlyHints().IsMatch(lower))
        {
            explicitPeriod = true;
            return Domain.SharedKernel.SalaryPeriod.Hourly;
        }

        if (DailyHints().IsMatch(lower))
        {
            explicitPeriod = true;
            return Domain.SharedKernel.SalaryPeriod.Daily;
        }

        explicitPeriod = false;
        return Domain.SharedKernel.SalaryPeriod.Monthly;
    }

    private static decimal? ParseAmount(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var cleaned = raw.Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace(",", ".", StringComparison.Ordinal)
            .Trim();

        if (!decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        // Guard against accidental matches such as "R$ 1" or metadata numbers.
        if (value < 100m || value > 10_000_000m)
        {
            return null;
        }

        return decimal.Round(value, 2, MidpointRounding.ToEven);
    }

    [GeneratedRegex(
        "R\\$\\s*(?<first>\\d[\\d.]*(?:,\\d{2})?)\\s*(?:-|–|—|a|até)\\s*R?\\$?\\s*(?<second>\\d[\\d.]*(?:,\\d{2})?)",
        RegexOptions.IgnoreCase,
        200)]
    private static partial Regex RangePattern();

    [GeneratedRegex("R\\$\\s*(?<value>\\d[\\d.]*(?:,\\d{2})?)", RegexOptions.IgnoreCase, 200)]
    private static partial Regex SinglePattern();

    [GeneratedRegex("(por ano|anual|ao ano|per year|annual|yearly)", RegexOptions.IgnoreCase, 200)]
    private static partial Regex YearlyHints();

    [GeneratedRegex("(por hora|por h\\b|hourly|per hour|/h\\b)", RegexOptions.IgnoreCase, 200)]
    private static partial Regex HourlyHints();

    [GeneratedRegex("(por dia|diária|diaria|daily|per day)", RegexOptions.IgnoreCase, 200)]
    private static partial Regex DailyHints();
}
