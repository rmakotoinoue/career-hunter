using System.Text.Json;
using CareerHunter.Application.Abstractions;
using CareerHunter.Domain.Candidates;
using CareerHunter.Domain.Discovery;
using Microsoft.Extensions.Logging;

namespace CareerHunter.Infrastructure.Adapters;

/// <summary>
/// Reads the public Lever postings API (https://github.com/lever/postings-api). No key is
/// required: the source URL points at one employer site, for example
/// <c>https://api.lever.co/v0/postings/acme?mode=json</c>.
/// Lever does not return the employer name, so the site token in the path is used unless the
/// source URL supplies a friendlier label with <c>?company=Nome</c>.
/// </summary>
public sealed class LeverJobSourceAdapter(
    IHttpClientFactory httpClientFactory,
    JobPostingFilter filter,
    ILogger<LeverJobSourceAdapter> logger) : IJobSourceAdapter
{
    public JobSourceKind Kind => JobSourceKind.Lever;

    public async Task<IReadOnlyList<CollectedJob>> CollectAsync(
        JobSource source,
        JobSearchPlan? searchPlan = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var baseUri = ProviderHttp.RequireHttps(new Uri(source.BaseUrl, UriKind.Absolute), "api.lever.co");
        var requestUri = ProviderHttp.WithQuery(baseUri, new Dictionary<string, string>
        {
            ["mode"] = "json"
        });

        using var client = httpClientFactory.CreateClient("job-sources");
        using var document = await ProviderHttp.GetJsonAsync(client, requestUri, "Lever", cancellationToken);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Lever returned an unexpected payload (expected a JSON array).");
        }

        var query = ProviderHttp.ParseQuery(requestUri);
        var company = query.TryGetValue("company", out var configured) && !string.IsNullOrWhiteSpace(configured)
            ? configured
            : SiteToken(requestUri);

        var collected = new List<CollectedJob>(document.RootElement.GetArrayLength());
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var job = ToCollectedJob(item, company);
            if (job is null || !filter.TitleMatches(job.Title, searchPlan?.Keywords) || !filter.LocationMatches(job.Location))
            {
                continue;
            }

            collected.Add(job);
        }

        logger.LogInformation(
            "Lever adapter collected {Count} job(s) from source {SourceId}.",
            collected.Count,
            source.Id);

        return collected;
    }

    private static CollectedJob? ToCollectedJob(JsonElement item, string? company)
    {
        var title = JsonFields.Text(item, "text");
        var hostedUrl = JsonFields.AbsoluteHttpUrl(JsonFields.Text(item, "hostedUrl"));
        var description = JobPostingText.ToPlainText(
            JsonFields.Text(item, "descriptionPlain") ?? JsonFields.Text(item, "description"));

        if (string.IsNullOrWhiteSpace(title) || hostedUrl is null || description.Length < 20)
        {
            return null;
        }

        return new CollectedJob(
            JsonFields.Identifier(item, "id"),
            hostedUrl.ToString(),
            JobPostingText.Truncate(title, 300),
            JobPostingText.Optional(company, 200),
            JobPostingText.Optional(JsonFields.NestedText(item, "categories", "location"), 200),
            description,
            JsonFields.FromUnixMilliseconds(item, "createdAt"));
    }

    /// <summary>Extracts the site token from /v0/postings/{site}.</summary>
    private static string? SiteToken(Uri uri)
    {
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 3
               && string.Equals(segments[0], "v0", StringComparison.OrdinalIgnoreCase)
               && string.Equals(segments[1], "postings", StringComparison.OrdinalIgnoreCase)
            ? segments[2]
            : null;
    }
}
