namespace CareerHunter.Application.Abstractions;

/// <summary>
/// Provider-agnostic completion port. Implementations live in Infrastructure; the domain
/// and business use cases never reference a specific AI vendor.
/// </summary>
public interface IAIProvider
{
    /// <summary>
    /// False when no credential/model is configured; callers then fall back to the
    /// deterministic engine instead of failing.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Model identifier configured for this provider; null when not configured.
    /// </summary>
    string? Model { get; }

    Task<AiCompletion> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default);
}

public sealed record AiCompletionRequest(
    string Model,
    string SystemPrompt,
    string UserPrompt,
    int MaxOutputTokens,
    double Temperature);

public sealed record AiCompletion(string Content, string Provider, string Model);

/// <summary>
/// E-mail port. Provider selection is an infrastructure concern.
/// </summary>
public interface IEmailSender
{
    string Provider { get; }

    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public sealed record EmailMessage(
    IReadOnlyList<string> To,
    string Subject,
    string TextBody,
    string HtmlBody);
