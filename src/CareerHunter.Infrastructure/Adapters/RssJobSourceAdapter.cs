using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using CareerHunter.Application.Abstractions;
using CareerHunter.Domain.Candidates;
using CareerHunter.Domain.Discovery;
using Microsoft.Extensions.Logging;

namespace CareerHunter.Infrastructure.Adapters;

/// <summary>
/// Reads a public RSS 2.0 or Atom feed. It only performs an anonymous GET on the configured
/// URL: no authentication bypass, no pagination crawling, no CAPTCHA handling.
/// The feed must expose an employer (custom element, author or creator); otherwise the raw
/// record is stored and rejected during normalization with an explicit reason.
/// </summary>
public sealed class RssJobSourceAdapter(
    IHttpClientFactory httpClientFactory,
    JobPostingFilter filter,
    ILogger<RssJobSourceAdapter> logger)
    : IJobSourceAdapter
{
    private const int MaximumDocumentBytes = 5 * 1024 * 1024;

    public JobSourceKind Kind => JobSourceKind.Rss;

    public async Task<IReadOnlyList<CollectedJob>> CollectAsync(
        JobSource source,
        JobSearchPlan? searchPlan = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var client = httpClientFactory.CreateClient("job-sources");
        using var response = await client.GetAsync(source.BaseUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength > MaximumDocumentBytes)
        {
            throw new InvalidOperationException("The feed exceeds the maximum accepted size.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            MaxCharactersInDocument = MaximumDocumentBytes
        };

        using var reader = XmlReader.Create(stream, settings);
        var document = XDocument.Load(reader, LoadOptions.None);

        var items = document
            .Descendants()
            .Where(element => element.Name.LocalName is "item" or "entry")
            .Select(ToCollectedJob)
            .Where(job => job is not null && filter.TitleMatches(job.Title, searchPlan?.Keywords))
            .Cast<CollectedJob>()
            .ToArray();

        logger.LogInformation(
            "RSS adapter collected {Count} item(s) from source {SourceId}.",
            items.Length,
            source.Id);

        return items;
    }

    private static CollectedJob? ToCollectedJob(XElement item)
    {
        var isAtom = item.Name.LocalName == "entry";

        var title = ChildValue(item, "title")?.Trim();
        var url = isAtom ? AtomLink(item) : ChildValue(item, "link")?.Trim();

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var description =
            ChildValue(item, "encoded")
            ?? ChildValue(item, "description")
            ?? ChildValue(item, "content")
            ?? ChildValue(item, "summary")
            ?? string.Empty;

        var company =
            ChildValue(item, "company")
            ?? ChildValue(item, "companyName")
            ?? ChildValue(item, "employer")
            ?? ChildValue(item, "author")
            ?? ChildValue(item, "creator");

        var location = ChildValue(item, "location") ?? ChildValue(item, "region");

        var externalId =
            ChildValue(item, "guid")
            ?? ChildValue(item, "id")
            ?? url;

        var publishedAt = ParseDate(
            ChildValue(item, "pubDate")
            ?? ChildValue(item, "published")
            ?? ChildValue(item, "updated")
            ?? ChildValue(item, "date"));

        return new CollectedJob(
            externalId,
            url,
            title,
            string.IsNullOrWhiteSpace(company) ? null : company.Trim(),
            string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
            description,
            publishedAt);
    }

    private static string? ChildValue(XElement parent, string localName)
        => parent
            .Elements()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))
            ?.Value;

    private static string? AtomLink(XElement entry)
        => entry
            .Elements()
            .Where(element => element.Name.LocalName == "link")
            .FirstOrDefault(element =>
            {
                var rel = element.Attribute("rel")?.Value;
                return string.IsNullOrWhiteSpace(rel) || string.Equals(rel, "alternate", StringComparison.OrdinalIgnoreCase);
            })
            ?.Attribute("href")
            ?.Value;

    private static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value.Trim(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }
}
