using System.Text.RegularExpressions;
using CareerHunter.Application.Common;
using CareerHunter.Domain.Catalog;
using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Application.Catalog;

public sealed record NormalizedJobData(
    string Title,
    JobLocation Location,
    WorkModality? Modality,
    EmploymentContractType? ContractType,
    SeniorityLevel? Seniority,
    JobCompensation? Compensation,
    string Description,
    IReadOnlyList<JobRequirementInput> Requirements);

/// <summary>
/// Deterministic normalization of raw source content into catalog data.
/// Every inferred field is left null when the description is not explicit, and salary parsing
/// is conservative: unknown salary stays unknown.
/// </summary>
public sealed class JobNormalizer(JobRequirementExtractor requirementExtractor)
{
    private static readonly Regex RemotePattern = new(
        "\\b(100\\s*%\\s*remoto|remoto|remota|remote|home\\s*office|trabalho\\s*remoto)\\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex HybridPattern = new(
        "\\b(h[íi]brido|h[íi]brida|hybrid|modelo\\s*h[íi]brido)\\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex OnsitePattern = new(
        "\\b(presencial|on-?site|onsite)\\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex PjPattern = new(
        "\\b(pj|pessoa\\s*jur[íi]dica)\\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex CltPattern = new(
        "\\bclt\\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex StatePattern = new(
        "^[A-Za-z]{2}$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public NormalizedJobData Normalize(string rawTitle, string? rawLocation, string rawDescription)
    {
        var description = HtmlTextSanitizer.ToPlainText(rawDescription);
        if (description.Length < 20)
        {
            throw new DomainValidationException("description", "Job description is too short after normalization.");
        }

        var title = HtmlTextSanitizer.ToPlainText(rawTitle, 200);
        if (title.Length < 3)
        {
            throw new DomainValidationException("title", "Job title is too short after normalization.");
        }

        var (city, state) = ParseLocation(rawLocation, description);
        var salary = SalaryParser.TryParse(description, out _);

        return new NormalizedJobData(
            title,
            JobLocation.Create(city, state),
            DetectModality(description),
            DetectContract(description),
            DetectSeniority($"{title}\n{description}"),
            salary is null
                ? null
                : JobCompensation.Create(
                    salary.MinAmount,
                    salary.MaxAmount,
                    salary.Period,
                    salary.Currency,
                    salary.IsEstimated),
            description,
            requirementExtractor.Extract(description));
    }

    private static WorkModality? DetectModality(string description)
    {
        // Hybrid wins over remote because hybrid postings usually mention remote days too.
        if (HybridPattern.IsMatch(description))
        {
            return WorkModality.Hybrid;
        }

        if (RemotePattern.IsMatch(description))
        {
            return WorkModality.Remote;
        }

        if (OnsitePattern.IsMatch(description))
        {
            return WorkModality.Onsite;
        }

        return null;
    }

    private static EmploymentContractType? DetectContract(string description)
    {
        var pj = PjPattern.IsMatch(description);
        var clt = CltPattern.IsMatch(description);

        return (pj, clt) switch
        {
            (true, false) => EmploymentContractType.Pj,
            (false, true) => EmploymentContractType.Clt,
            _ => null
        };
    }

    private static SeniorityLevel? DetectSeniority(string text)
    {
        if (Regex.IsMatch(text, "\\barquiteto\\b|\\barchitect\\b", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
        {
            return SeniorityLevel.Architect;
        }

        if (Regex.IsMatch(text, "\\btech\\s*lead\\b|\\blead\\b|lideran[çc]a\\s*t[ée]cnica", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
        {
            return SeniorityLevel.Lead;
        }

        if (Regex.IsMatch(text, "\\bprincipal\\b", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
        {
            return SeniorityLevel.Principal;
        }

        if (Regex.IsMatch(text, "\\bs[êe]nior\\b|\\bsenior\\b|\\bsr\\b", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
        {
            return SeniorityLevel.Senior;
        }

        if (Regex.IsMatch(text, "\\bpleno\\b|\\bmid[- ]?level\\b", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
        {
            return SeniorityLevel.MidLevel;
        }

        if (Regex.IsMatch(text, "\\bj[úu]nior\\b|\\bjunior\\b|\\bjr\\b", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
        {
            return SeniorityLevel.Junior;
        }

        if (Regex.IsMatch(text, "\\best[áa]gio\\b|\\bestagi[áa]rio\\b|\\bintern\\b", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
        {
            return SeniorityLevel.Intern;
        }

        return null;
    }

    private static (string? City, string? State) ParseLocation(string? rawLocation, string description)
    {
        if (string.IsNullOrWhiteSpace(rawLocation))
        {
            return (null, null);
        }

        var normalized = HtmlTextSanitizer.ToPlainText(rawLocation, 200);
        if (normalized.Length == 0
            || RemotePattern.IsMatch(normalized)
            || normalized.Contains("remote", StringComparison.OrdinalIgnoreCase))
        {
            return (null, null);
        }

        var parts = normalized
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var city = parts.Length > 0 ? parts[0] : null;
        string? state = null;

        if (parts.Length > 1 && StatePattern.IsMatch(parts[1]))
        {
            state = parts[1].ToUpperInvariant();
        }
        else if (parts.Length > 1 && !parts[1].Equals("Brazil", StringComparison.OrdinalIgnoreCase)
                                && !parts[1].Equals("Brasil", StringComparison.OrdinalIgnoreCase))
        {
            city = string.Join(", ", parts.Take(parts.Length - 1));
            state = parts[^1];
        }

        if (city is not null && city.Length > 120)
        {
            city = city[..120];
        }

        return (city, state);
    }
}
