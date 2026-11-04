using System.Text.RegularExpressions;
using CareerHunter.Domain.Catalog;
using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Application.Catalog;

/// <summary>
/// Extracts concrete requirements from a job description. The extractor only recognizes
/// explicit technology/education/language terms; it never invents a requirement, and an
/// empty list is a valid outcome.
/// </summary>
public sealed class JobRequirementExtractor
{
    private const int MaxRequirements = 40;

    private static readonly (string Pattern, string Display, JobRequirementCategory Category)[] KnownTerms =
    [
        ("c#", "C#", JobRequirementCategory.Technology),
        (".net", ".NET", JobRequirementCategory.Technology),
        ("asp.net core", "ASP.NET Core", JobRequirementCategory.Technology),
        ("asp.net", "ASP.NET", JobRequirementCategory.Technology),
        ("entity framework", "Entity Framework", JobRequirementCategory.Technology),
        ("sql server", "SQL Server", JobRequirementCategory.Technology),
        ("postgresql", "PostgreSQL", JobRequirementCategory.Technology),
        ("postgres", "PostgreSQL", JobRequirementCategory.Technology),
        ("mysql", "MySQL", JobRequirementCategory.Technology),
        ("oracle", "Oracle", JobRequirementCategory.Technology),
        ("mongodb", "MongoDB", JobRequirementCategory.Technology),
        ("redis", "Redis", JobRequirementCategory.Technology),
        ("rabbitmq", "RabbitMQ", JobRequirementCategory.Technology),
        ("kafka", "Kafka", JobRequirementCategory.Technology),
        ("docker", "Docker", JobRequirementCategory.Technology),
        ("kubernetes", "Kubernetes", JobRequirementCategory.Technology),
        ("terraform", "Terraform", JobRequirementCategory.Technology),
        ("aws", "AWS", JobRequirementCategory.Technology),
        ("azure", "Azure", JobRequirementCategory.Technology),
        ("gcp", "Google Cloud", JobRequirementCategory.Technology),
        ("microservices", "Microservices", JobRequirementCategory.Technology),
        ("microsserviços", "Microservices", JobRequirementCategory.Technology),
        ("rest api", "REST APIs", JobRequirementCategory.Technology),
        ("restful", "REST APIs", JobRequirementCategory.Technology),
        ("apis rest", "REST APIs", JobRequirementCategory.Technology),
        ("graphql", "GraphQL", JobRequirementCategory.Technology),
        ("grpc", "gRPC", JobRequirementCategory.Technology),
        ("angular", "Angular", JobRequirementCategory.Technology),
        ("react", "React", JobRequirementCategory.Technology),
        ("typescript", "TypeScript", JobRequirementCategory.Technology),
        ("javascript", "JavaScript", JobRequirementCategory.Technology),
        ("node.js", "Node.js", JobRequirementCategory.Technology),
        ("python", "Python", JobRequirementCategory.Technology),
        ("java", "Java", JobRequirementCategory.Technology),
        ("kotlin", "Kotlin", JobRequirementCategory.Technology),
        ("golang", "Go", JobRequirementCategory.Technology),
        ("ddd", "Domain-Driven Design", JobRequirementCategory.Technology),
        ("tdd", "TDD", JobRequirementCategory.Technology),
        ("ci/cd", "CI/CD", JobRequirementCategory.Technology),
        ("git", "Git", JobRequirementCategory.Technology),
        ("linux", "Linux", JobRequirementCategory.Technology),
        ("scrum", "Scrum", JobRequirementCategory.Other),
        ("kanban", "Kanban", JobRequirementCategory.Other),
        ("inglês", "Inglês", JobRequirementCategory.Language),
        ("ingles", "Inglês", JobRequirementCategory.Language),
        ("english", "Inglês", JobRequirementCategory.Language),
        ("espanhol", "Espanhol", JobRequirementCategory.Language),
        ("spanish", "Espanhol", JobRequirementCategory.Language),
        ("ensino superior", "Ensino superior", JobRequirementCategory.Education),
        ("graduação", "Graduação", JobRequirementCategory.Education),
        ("bacharelado", "Bacharelado", JobRequirementCategory.Education)
    ];

    private static readonly Regex[] CompiledTerms = KnownTerms
        .Select(term => new Regex(
            $"(?<![\\p{{L}}\\p{{N}}]){Regex.Escape(term.Pattern)}(?![\\p{{L}}\\p{{N}}])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100)))
        .ToArray();

    private static readonly Regex RequiredHeading = new(
        "(requisitos|requirements|obrigat[óo]rio|necess[áa]rio|must have|qualifica[çc][õo]es)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex PreferredHeading = new(
        "(desej[áa]ve(?:l|is)|diferencia(?:l|is)|nice to have|preferred|plus|opciona(?:l|is))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public IReadOnlyList<JobRequirementInput> Extract(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return [];
        }

        var requirements = new Dictionary<string, JobRequirementInput>(StringComparer.Ordinal);
        var preferredSections = false;

        var lines = description.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            // Section state persists until the next heading so items listed after
            // "Requisitos:" or "Diferenciais:" keep the correct classification.
            if (RequiredHeading.IsMatch(line))
            {
                preferredSections = false;
            }
            else if (PreferredHeading.IsMatch(line))
            {
                preferredSections = true;
            }

            for (var index = 0; index < CompiledTerms.Length; index++)
            {
                if (!CompiledTerms[index].IsMatch(line))
                {
                    continue;
                }

                var term = KnownTerms[index];
                var key = TextNormalizer.ForMatching(term.Display);
                var isRequired = !preferredSections;

                if (requirements.TryGetValue(key, out var existing))
                {
                    if (isRequired && !existing.IsRequired)
                    {
                        requirements[key] = existing with { IsRequired = true, RawText = line };
                    }

                    continue;
                }

                requirements[key] = new JobRequirementInput(
                    term.Display,
                    term.Category,
                    isRequired,
                    line.Length > 2000 ? line[..2000] : line);

                if (requirements.Count >= MaxRequirements)
                {
                    return requirements.Values.ToArray();
                }
            }
        }

        return requirements.Values.ToArray();
    }
}
