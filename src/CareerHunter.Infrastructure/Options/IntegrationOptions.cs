namespace CareerHunter.Infrastructure.Options;

/// <summary>
/// Settings for the OpenAI-compatible chat completions endpoint. The provider is optional:
/// when it is not configured the assessment context falls back to the deterministic engine.
/// </summary>
public sealed class AiOptions
{
    public string? BaseUrl { get; init; }

    public string? ApiKey { get; init; }

    public string? Model { get; init; }

    /// <summary>
    /// Some OpenAI-compatible servers do not implement response_format. Disable it there.
    /// </summary>
    public bool UseJsonResponseFormat { get; init; } = true;

    public int TimeoutSeconds { get; init; } = 60;
}

/// <summary>
/// SMTP settings. When <see cref="Host"/> is empty the e-mail port degrades to a logging
/// implementation so local runs never attempt to contact a mail server.
/// </summary>
public sealed class EmailOptions
{
    public string? Host { get; init; }

    public int Port { get; init; } = 587;

    public bool UseStartTls { get; init; } = true;

    public string? UserName { get; init; }

    public string? Password { get; init; }

    public string? FromAddress { get; init; }

    public string FromName { get; init; } = "Career Hunter";

    public int TimeoutSeconds { get; init; } = 30;
}
