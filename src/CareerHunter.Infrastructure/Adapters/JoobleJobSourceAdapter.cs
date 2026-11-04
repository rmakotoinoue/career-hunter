using System.Text.Json;
using CareerHunter.Application.Abstractions;
using CareerHunter.Domain.Candidates;
using CareerHunter.Domain.Discovery;
using CareerHunter.Infrastructure.Options;
using Microsoft.Extensions.Logging;

namespace CareerHunter.Infrastructure.Adapters;

/// <summary>
/// Reads the official Jooble API (https://jooble.org/api/about). Jooble authenticates through
/// the path of the endpoint, so the key is taken from configuration, never from the source URL,
/// and provider errors are reported without echoing the URL.
/// The endpoint host comes from the registered source, so the global site (<c>jooble.org</c>) and
/// the Brazilian front (<c>br.jooble.org</c>) both work; only those hosts are accepted.
/// When the source belongs to a candidate, the post body is rebuilt from the profile on every
/// run; otherwise the query registered in the URL is kept, for example
/// <c>https://br.jooble.org/api/?keywords=desenvolvedor%20.net&amp;location=Brasil</c>.
/// </summary>
public sealed class JoobleJobSourceAdapter(
    IHttpClientFactory httpClientFactory,
    JobProviderOptions options,
    JobPostingFilter filter,
    ILogger<JoobleJobSourceAdapter> logger) : IJobSourceAdapter
{
    private const string DefaultLocation = "Brasil";

    public JobSourceKind Kind => JobSourceKind.Jooble;

    public async Task<IReadOnlyList<CollectedJob>> CollectAsync(
        JobSource source,
        JobSearchPlan? searchPlan = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!options.Jooble.IsConfigured)
        {
            throw new InvalidOperationException("Jooble is not configured. Set JobProviders__Jooble__ApiKey.");
        }

        var sourceUri = ProviderHttp.RequireHttps(
            new Uri(source.BaseUrl, UriKind.Absolute),
            "jooble.org",
            "www.jooble.org",
            "br.jooble.org");

        string keywords;
        string location;

        if (searchPlan is { IsEmpty: false } && searchPlan.Query.Length > 0)
        {
            keywords = searchPlan.Query;
            location = string.IsNullOrWhiteSpace(searchPlan.ProviderLocation)
                ? DefaultLocation
                : searchPlan.ProviderLocation;
        }
        else
        {
            var query = ProviderHttp.ParseQuery(sourceUri);
            if (!query.TryGetValue("keywords", out var configured) || string.IsNullOrWhiteSpace(configured))
            {
                throw new InvalidOperationException(
                    "The Jooble source URL must include a 'keywords' query parameter, for example "
                    + "https://br.jooble.org/api/?keywords=desenvolvedor%20.net&location=Brasil.");
            }

            keywords = configured;
            location = query.TryGetValue("location", out var configuredLocation)
                       && !string.IsNullOrWhiteSpace(configuredLocation)
                ? configuredLocation
                : DefaultLocation;
        }

        var endpoint = new Uri($"{sourceUri.Scheme}://{sourceUri.Host}/api/{Uri.EscapeDataString(options.Jooble.ApiKey!)}");
        var payload = JsonSerializer.Serialize(new
        {
            keywords,
            location,
            page = "1"
        });

        using var client = httpClientFactory.CreateClient("job-sources");
        using var document = await ProviderHttp.PostJsonAsync(client, endpoint, payload, "Jooble", cancellationToken);

        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("jobs", out var jobs)
            || jobs.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Jooble returned an unexpected payload (missing 'jobs' array).");
        }

        var collected = new List<CollectedJob>(jobs.GetArrayLength());
        foreach (var item in jobs.EnumerateArray())
        {
            var job = ToCollectedJob(item);
            if (job is null || !filter.TitleMatches(job.Title, searchPlan?.Keywords))
            {
                continue;
            }

            collected.Add(job);
        }

        logger.LogInformation(
            "Jooble adapter collected {Count} job(s) from source {SourceId}.",
            collected.Count,
            source.Id);

        return collected;
    }

    private static CollectedJob? ToCollectedJob(JsonElement item)
    {
        var title = JsonFields.Text(item, "title");
        var link = JsonFields.Text(item, "link");
        var description = JobPostingText.ToPlainText(JsonFields.Text(item, "snippet"));

        var url = JsonFields.AbsoluteHttpUrl(link);
        if (string.IsNullOrWhiteSpace(title) || url is null || description.Length < 20)
        {
            return null;
        }

        return new CollectedJob(
            JsonFields.Identifier(item, "id"),
            url.ToString(),
            JobPostingText.Truncate(title, 300),
            JobPostingText.Optional(JsonFields.Text(item, "company"), 200),
            JobPostingText.Optional(JsonFields.Text(item, "location"), 200),
            description,
            JsonFields.Date(item, "updated"));
    }
}
