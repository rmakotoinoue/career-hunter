using System.Security.Cryptography;
using System.Text;

namespace CareerHunter.Api;

/// <summary>
/// Optional shared-secret protection for the HTTP API. The candidate profile is personal data and
/// the evaluation endpoint spends money on a provider key, so the API must not be reachable by
/// anyone who can route to the port.
/// <para>
/// When <c>Api:ApiKey</c> is empty the filter is a no-op: that is only acceptable for a local run
/// bound to the loopback interface. <c>docker-compose.yml</c> publishes the API on 127.0.0.1 and
/// passes <c>Api__ApiKey</c>, so a VPS deployment adds a key without extra infrastructure.
/// </para>
/// </summary>
public sealed class ApiKeyOptions
{
    public string? ApiKey { get; init; }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(ApiKey);
}

public sealed class ApiKeyEndpointFilter(ApiKeyOptions options) : IEndpointFilter
{
    public const string HeaderName = "X-Api-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!options.IsEnabled)
        {
            return await next(context);
        }

        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();

        if (!IsValid(provided))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: $"A valid {HeaderName} header is required.");
        }

        return await next(context);
    }

    private bool IsValid(string provided)
    {
        var expected = Encoding.UTF8.GetBytes(options.ApiKey!);
        var actual = Encoding.UTF8.GetBytes(provided);

        // FixedTimeEquals compares in constant time and returns false for different lengths.
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
