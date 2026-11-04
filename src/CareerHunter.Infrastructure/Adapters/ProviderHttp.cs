using System.Net;
using System.Text;
using System.Text.Json;

namespace CareerHunter.Infrastructure.Adapters;

/// <summary>
/// Shared plumbing for the board APIs: HTTPS-only hosts, bounded responses and messages that
/// never echo the request URL (Jooble carries its key in the path, so a naive exception would
/// leak the secret into logs and collection runs).
/// </summary>
internal static class ProviderHttp
{
    private const int MaximumResponseBytes = 10 * 1024 * 1024;

    public static Uri RequireHttps(Uri uri, params string[] allowedHosts)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The job source URL must use HTTPS.");
        }

        if (!allowedHosts.Any(host => string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"The job source host '{uri.Host}' is not the documented {allowedHosts[0]} endpoint.");
        }

        return uri;
    }

    public static async Task<JsonDocument> GetJsonAsync(
        HttpClient client,
        Uri uri,
        string provider,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        return await ReadJsonAsync(response, provider, cancellationToken);
    }

    public static async Task<JsonDocument> PostJsonAsync(
        HttpClient client,
        Uri uri,
        string json,
        string provider,
        CancellationToken cancellationToken)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(uri, content, cancellationToken);
        return await ReadJsonAsync(response, provider, cancellationToken);
    }

    public static Dictionary<string, string> ParseQuery(Uri uri)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var query = uri.Query.TrimStart('?');
        if (query.Length == 0)
        {
            return result;
        }

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var key = separator < 0 ? pair : pair[..separator];
            var value = separator < 0 ? string.Empty : pair[(separator + 1)..];

            result[Decode(key)] = Decode(value);
        }

        return result;
    }

    public static Uri WithQuery(Uri uri, IEnumerable<KeyValuePair<string, string>> parameters)
    {
        var merged = ParseQuery(uri);
        foreach (var (key, value) in parameters)
        {
            merged[key] = value;
        }

        var query = string.Join(
            '&',
            merged.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

        return new UriBuilder(uri) { Query = query }.Uri;
    }

    private static string Decode(string value)
        => Uri.UnescapeDataString(value.Replace("+", " ", StringComparison.Ordinal));

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response,
        string provider,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"{provider} returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase ?? "no reason"}).");
        }

        if (response.Content.Headers.ContentLength > MaximumResponseBytes)
        {
            throw new InvalidOperationException($"{provider} returned a response above the maximum accepted size.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        try
        {
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"{provider} returned a payload that is not valid JSON.", exception);
        }
    }
}
