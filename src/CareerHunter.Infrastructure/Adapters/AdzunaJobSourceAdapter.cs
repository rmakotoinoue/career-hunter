using System.Globalization;
using System.Text.Json;
using CareerHunter.Application.Abstractions;
using CareerHunter.Domain.Candidates;
using CareerHunter.Domain.Discovery;
using CareerHunter.Infrastructure.Options;
using Microsoft.Extensions.Logging;

namespace CareerHunter.Infrastructure.Adapters;

/// <summary>
/// Reads the official Adzuna search API (https://developer.adzuna.com/docs/search).
/// The registered source URL carries the endpoint (country path); the application id and key
/// come from configuration and are appended only to the outgoing request, never stored in the
/// database. When the source belongs to a candidate, <c>what</c> and <c>where</c> are rebuilt from
/// the profile on every run; otherwise the query registered in the URL is kept.
/// </summary>
public sealed class AdzunaJobSourceAdapter(
    IHttpClientFactory httpClientFactory,
    JobProviderOptions options,
    JobPostingFilter filter,
    ILogger<AdzunaJobSourceAdapter> logger) : IJobSourceAdapter
{
    public JobSourceKind Kind => JobSourceKind.Adzuna;

    public async Task<IReadOnlyList<CollectedJob>> CollectAsync(
        JobSource source,
        JobSearchPlan? searchPlan = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!options.Adzuna.IsConfigured)
        {
            throw new InvalidOperationException(
                "Adzuna is not configured. Set JobProviders__Adzuna__AppId and JobProviders__Adzuna__AppKey.");
        }

        var baseUri = ProviderHttp.RequireHttps(new Uri(source.BaseUrl, UriKind.Absolute), "api.adzuna.com");

        var search = ProviderHttp.ParseQuery(baseUri);
        if (searchPlan is { IsEmpty: false })
        {
            // The profile owns what/where; other parameters registered in the URL (category,
            // salary filters, page) are preserved. what_or keeps the skill group as OR, because
            // Adzuna ANDs the words inside each parameter.
            search.Remove("what");
            search.Remove("what_or");
            search.Remove("where");

            if (searchPlan.TitleQuery.Length > 0)
            {
                search["what"] = searchPlan.TitleQuery;
            }

            if (searchPlan.SkillQuery.Length > 0)
            {
                search["what_or"] = searchPlan.SkillQuery;
            }

            if (searchPlan.GeographicFilter is { Length: > 0 } where)
            {
                search["where"] = where;
            }
        }

        search["app_id"] = options.Adzuna.AppId!;
        search["app_key"] = options.Adzuna.AppKey!;
        search["results_per_page"] = Math.Clamp(options.Adzuna.ResultsPerPage, 1, 50)
            .ToString(CultureInfo.InvariantCulture);

        var requestUri = ProviderHttp.WithQuery(new UriBuilder(baseUri) { Query = string.Empty }.Uri, search);

        using var client = httpClientFactory.CreateClient("job-sources");
        using var document = await ProviderHttp.GetJsonAsync(client, requestUri, "Adzuna", cancellationToken);

        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("results", out var results)
            || results.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Adzuna returned an unexpected payload (missing 'results' array).");
        }

        var collected = new List<CollectedJob>(results.GetArrayLength());
        foreach (var item in results.EnumerateArray())
        {
            var job = ToCollectedJob(item);
            if (job is null || !filter.TitleMatches(job.Title, searchPlan?.Keywords))
            {
                continue;
            }

            collected.Add(job);
        }

        logger.LogInformation(
            "Adzuna adapter collected {Count} job(s) from source {SourceId}.",
            collected.Count,
            source.Id);

        return collected;
    }

    private static CollectedJob? ToCollectedJob(JsonElement item)
    {
        var title = JsonFields.Text(item, "title");
        var redirect = JsonFields.Text(item, "redirect_url");
        var description = JobPostingText.ToPlainText(JsonFields.Text(item, "description"));

        var url = JsonFields.AbsoluteHttpUrl(redirect);
        if (string.IsNullOrWhiteSpace(title) || url is null || description.Length < 20)
        {
            return null;
        }

        return new CollectedJob(
            JsonFields.Identifier(item, "id"),
            url.ToString(),
            JobPostingText.Truncate(title, 300),
            JobPostingText.Optional(JsonFields.NestedText(item, "company", "display_name"), 200),
            JobPostingText.Optional(JsonFields.NestedText(item, "location", "display_name"), 200),
            description,
            JsonFields.Date(item, "created"));
    }
}
