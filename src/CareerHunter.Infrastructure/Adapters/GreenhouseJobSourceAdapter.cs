using System.Text.Json;
using CareerHunter.Application.Abstractions;
using CareerHunter.Domain.Candidates;
using CareerHunter.Domain.Discovery;
using Microsoft.Extensions.Logging;

namespace CareerHunter.Infrastructure.Adapters;

/// <summary>
/// Reads the public Greenhouse job board API
/// (https://developers.greenhouse.io/job-board.html). No key is required: the source URL points
/// at the board of one employer, for example
/// <c>https://boards-api.greenhouse.io/v1/boards/c6bank/jobs?content=true</c>.
/// The board returns every open position, so the title/location filter trims the result to the
/// roles the candidate tracks; the title keywords come from the candidate profile when the
/// source belongs to one.
/// </summary>
public sealed class GreenhouseJobSourceAdapter(
    IHttpClientFactory httpClientFactory,
    JobPostingFilter filter,
    ILogger<GreenhouseJobSourceAdapter> logger) : IJobSourceAdapter
{
    public JobSourceKind Kind => JobSourceKind.Greenhouse;

    public async Task<IReadOnlyList<CollectedJob>> CollectAsync(
        JobSource source,
        JobSearchPlan? searchPlan = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var baseUri = ProviderHttp.RequireHttps(new Uri(source.BaseUrl, UriKind.Absolute), "boards-api.greenhouse.io");
        var requestUri = ProviderHttp.WithQuery(baseUri, new Dictionary<string, string>
        {
            ["content"] = "true"
        });

        using var client = httpClientFactory.CreateClient("job-sources");
        using var document = await ProviderHttp.GetJsonAsync(client, requestUri, "Greenhouse", cancellationToken);

        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("jobs", out var jobs)
            || jobs.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Greenhouse returned an unexpected payload (missing 'jobs' array).");
        }

        var boardToken = BoardToken(requestUri);
        var collected = new List<CollectedJob>(jobs.GetArrayLength());
        var missingCompany = 0;

        foreach (var item in jobs.EnumerateArray())
        {
            var job = ToCollectedJob(item, boardToken, ref missingCompany);
            if (job is null || !filter.TitleMatches(job.Title, searchPlan?.Keywords) || !filter.LocationMatches(job.Location))
            {
                continue;
            }

            collected.Add(job);
        }

        if (missingCompany > 0)
        {
            logger.LogWarning(
                "Greenhouse board {Board} omitted company_name for {Count} job(s); the board token was used as employer.",
                boardToken,
                missingCompany);
        }

        logger.LogInformation(
            "Greenhouse adapter collected {Count} job(s) from source {SourceId}.",
            collected.Count,
            source.Id);

        return collected;
    }

    private static CollectedJob? ToCollectedJob(JsonElement item, string? boardToken, ref int missingCompany)
    {
        var title = JsonFields.Text(item, "title");
        var absoluteUrl = JsonFields.AbsoluteHttpUrl(JsonFields.Text(item, "absolute_url"));
        var description = JobPostingText.ToPlainText(JsonFields.Text(item, "content"));

        if (string.IsNullOrWhiteSpace(title) || absoluteUrl is null || description.Length < 20)
        {
            return null;
        }

        var company = JsonFields.Text(item, "company_name");
        if (string.IsNullOrWhiteSpace(company))
        {
            company = boardToken;
            missingCompany++;
        }

        // first_published is the publication date; updated_at only says when the posting was
        // last edited, so it is a fallback rather than the truth.
        var publishedAt = JsonFields.Date(item, "first_published") ?? JsonFields.Date(item, "updated_at");

        return new CollectedJob(
            JsonFields.Identifier(item, "id"),
            absoluteUrl.ToString(),
            JobPostingText.Truncate(title, 300),
            JobPostingText.Optional(company, 200),
            JobPostingText.Optional(JsonFields.NestedText(item, "location", "name"), 200),
            description,
            publishedAt);
    }

    /// <summary>Extracts the board token from /v1/boards/{board}/jobs.</summary>
    private static string? BoardToken(Uri uri)
    {
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 3
               && string.Equals(segments[0], "v1", StringComparison.OrdinalIgnoreCase)
               && string.Equals(segments[1], "boards", StringComparison.OrdinalIgnoreCase)
            ? segments[2]
            : null;
    }
}
