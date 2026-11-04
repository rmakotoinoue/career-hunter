using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Domain.Candidates;

/// <summary>
/// Search terms derived deterministically from a candidate profile. It never invents data:
/// the query comes from the declared career targets plus the candidate's strongest technical
/// skills, and the location comes from the declared location and work preferences. The headline
/// contributes extra matching terms because boards often use the English wording of the same role.
/// The plan is rebuilt on every collection, so changing the profile changes the search on the
/// next run without touching the registered sources.
/// </summary>
public sealed class JobSearchPlan
{
    private const int MaxKeywords = 20;
    private const int MaxQuerySkills = 2;
    private const int MaxProfileSkills = 6;

    private static readonly string[] StopWords =
    [
        "a", "o", "as", "os", "de", "da", "do", "das", "dos", "e", "em", "para", "com", "por",
        "the", "and", "of", "for", "with", "full", "vaga", "vagas",
        "senior", "sênior", "pleno", "junior", "júnior", "jr", "sr",
        "remoto", "remota", "hibrido", "híbrido", "presencial", "pj", "clt"
    ];

    private static readonly char[] TokenSeparators =
    [
        ' ', '\t', '\r', '\n', '/', '|', ',', ';', '(', ')', '[', ']', '{', '}', ':', '"', '\''
    ];

    private JobSearchPlan(
        string titleQuery,
        string skillQuery,
        string query,
        IReadOnlyList<string> keywords,
        string? city,
        string? state,
        string country,
        bool acceptsRemote)
    {
        TitleQuery = titleQuery;
        SkillQuery = skillQuery;
        Query = query;
        Keywords = keywords;
        City = city;
        State = state;
        Country = country;
        AcceptsRemote = acceptsRemote;
    }

    /// <summary>
    /// Primary career target (or the headline when there is none). Adzuna applies AND across the
    /// words of <c>what</c>, so sending the skills in the same parameter would shrink the result to
    /// almost nothing ("Tech Lead .NET C#" returned 1 posting against 452 for "Tech Lead").
    /// </summary>
    public string TitleQuery { get; }

    /// <summary>Skills sent to providers that accept an OR group (Adzuna <c>what_or</c>).</summary>
    public string SkillQuery { get; }

    /// <summary>Free-text query for providers that match keywords loosely (Jooble).</summary>
    public string Query { get; }

    /// <summary>Terms used to trim boards that always return their whole catalog.</summary>
    public IReadOnlyList<string> Keywords { get; }

    public string? City { get; }

    public string? State { get; }

    public string Country { get; }

    public bool AcceptsRemote { get; }

    public bool IsEmpty => Query.Length == 0 && Keywords.Count == 0;

    /// <summary>"City, State" or "City"; null when the candidate did not declare a city.</summary>
    public string? CityLocation => City is null
        ? null
        : string.IsNullOrWhiteSpace(State) ? City : $"{City}, {State}";

    /// <summary>
    /// Location to send when the provider searches by place. A candidate who accepts remote work
    /// is searched in the whole country: filtering by their own city would drop every remote and
    /// hybrid opening from other cities.
    /// </summary>
    public string ProviderLocation => AcceptsRemote || CityLocation is null ? Country : CityLocation;

    /// <summary>
    /// City-level filter for providers already scoped by country (Adzuna's /br/ path). Null when
    /// the search is nationwide, because a city filter would hide remote postings.
    /// </summary>
    public string? GeographicFilter => AcceptsRemote ? null : CityLocation;

    public static JobSearchPlan FromProfile(CandidateProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var titles = profile.CareerTargets
            .OrderByDescending(target => target.IsPrimary)
            .ThenBy(target => target.JobTitle, StringComparer.OrdinalIgnoreCase)
            .Select(target => target.JobTitle.Trim())
            .ToList();

        if (titles.Count == 0 && !string.IsNullOrWhiteSpace(profile.Headline))
        {
            titles.Add(profile.Headline.Trim());
        }

        var skills = profile.Skills
            .Where(skill => skill.Category is SkillCategory.Technical or SkillCategory.Domain)
            .OrderByDescending(skill => skill.Proficiency)
            .ThenByDescending(skill => skill.YearsOfExperience)
            .ThenBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)
            .Select(skill => skill.Name.Trim())
            .ToList();

        var keywords = new List<string>();
        foreach (var title in titles)
        {
            Add(keywords, title);
            foreach (var token in Tokenize(title))
            {
                Add(keywords, token);
            }
        }

        // The headline describes the same role family, often with the English wording that the
        // boards use ("Engenheiro de Software" versus "Software Engineer"), so it widens recall
        // without introducing terms the candidate did not declare.
        if (!string.IsNullOrWhiteSpace(profile.Headline))
        {
            foreach (var token in Tokenize(profile.Headline))
            {
                Add(keywords, token);
            }
        }

        foreach (var skill in skills.Take(MaxProfileSkills))
        {
            Add(keywords, skill);
        }

        var primaryTitle = titles.FirstOrDefault() ?? string.Empty;
        var skillQuery = string.Join(' ', skills.Where(skill => skill.Length > 0).Take(MaxQuerySkills));
        var query = string.Join(' ', new[] { primaryTitle, skillQuery }.Where(part => part.Length > 0));

        var acceptsRemote = profile.WorkPreferences?.AcceptsRemote ?? false;

        return new JobSearchPlan(
            primaryTitle.Trim(),
            skillQuery,
            query,
            keywords.Take(MaxKeywords).ToArray(),
            profile.Location.City.Trim(),
            profile.Location.State?.Trim(),
            profile.Location.Country.Trim(),
            acceptsRemote);
    }

    private static void Add(List<string> keywords, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (!keywords.Contains(normalized, StringComparer.Ordinal))
        {
            keywords.Add(normalized);
        }
    }

    private static IEnumerable<string> Tokenize(string value)
    {
        foreach (var raw in value.ToLowerInvariant().Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.TrimEnd('.', ',', ';', ':', '!', '?', ')', '(');
            if (token.Length == 0 || StopWords.Contains(token, StringComparer.Ordinal))
            {
                continue;
            }

            // Two-character tokens are noise ("go", "bi") except for C#, which is a real skill.
            if (token.Length >= 4 || token == "c#")
            {
                yield return token;
            }
        }
    }
}
