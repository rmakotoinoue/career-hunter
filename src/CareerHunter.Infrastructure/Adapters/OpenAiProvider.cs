using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CareerHunter.Application.Abstractions;
using CareerHunter.Infrastructure.Options;
using Microsoft.Extensions.Logging;

namespace CareerHunter.Infrastructure.Adapters;

/// <summary>
/// Minimal OpenAI-compatible chat completions client. Only the chat completions contract is
/// used so the implementation also works with self-hosted compatible servers.
/// Any failure is surfaced as an exception; <see cref="Application.Assessment.AiEvaluationEngine"/>
/// converts it into a deterministic fallback instead of failing the request.
/// </summary>
public sealed class OpenAiProvider : IAIProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly AiOptions _options;
    private readonly ILogger<OpenAiProvider> _logger;
    private readonly Uri? _completionEndpoint;

    public OpenAiProvider(HttpClient httpClient, AiOptions options, ILogger<OpenAiProvider> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;

        if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            var normalized = baseUri.AbsoluteUri.EndsWith('/') ? baseUri : new Uri(baseUri.AbsoluteUri + "/");
            _completionEndpoint = new Uri(normalized, "chat/completions");
        }
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.ApiKey)
        && !string.IsNullOrWhiteSpace(_options.Model)
        && _completionEndpoint is not null;

    public string? Model => IsConfigured ? _options.Model : null;

    public async Task<AiCompletion> CompleteAsync(
        AiCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsConfigured)
        {
            throw new InvalidOperationException("The AI provider is not configured.");
        }

        var payload = new
        {
            model = request.Model,
            temperature = request.Temperature,
            max_tokens = request.MaxOutputTokens,
            response_format = _options.UseJsonResponseFormat ? new { type = "json_object" } : null,
            messages = new object[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt }
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _completionEndpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "AI provider returned HTTP {StatusCode}: {Body}",
                (int)response.StatusCode,
                Truncate(body, 500));
            throw new HttpRequestException(
                $"The AI provider returned HTTP {(int)response.StatusCode}.",
                inner: null,
                response.StatusCode);
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (!root.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("The AI provider returned no choices.");
        }

        var message = choices[0].GetProperty("message");
        var content = message.TryGetProperty("content", out var contentElement)
            ? contentElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("The AI provider returned an empty message.");
        }

        var model = root.TryGetProperty("model", out var modelElement)
            ? modelElement.GetString() ?? request.Model
            : request.Model;

        return new AiCompletion(content, "openai-compatible", model);
    }

    private static string Truncate(string value, int maximum)
        => value.Length <= maximum ? value : value[..maximum];
}
