using Microsoft.Extensions.Configuration;

namespace CareerHunter.Worker;

/// <summary>
/// Background pipeline cadence. All values are overridable through configuration or
/// environment variables (for example <c>Worker__CycleIntervalMinutes=15</c>).
/// </summary>
public sealed class WorkerOptions
{
    public bool RunOnStartup { get; init; } = true;

    public int CycleIntervalMinutes { get; init; } = 30;

    public bool CollectForce { get; init; }

    public int NormalizeTake { get; init; } = 100;

    public int EvaluateTake { get; init; } = 10;

    public bool UseAi { get; init; } = true;

    public bool SendDigests { get; init; } = true;

    public string TimeZoneId { get; init; } = "America/Sao_Paulo";

    public static WorkerOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection("Worker");

        return new WorkerOptions
        {
            RunOnStartup = Boolean(section, "RunOnStartup", fallback: true),
            CycleIntervalMinutes = Integer(section, "CycleIntervalMinutes", fallback: 30),
            CollectForce = Boolean(section, "CollectForce", fallback: false),
            NormalizeTake = Integer(section, "NormalizeTake", fallback: 100),
            EvaluateTake = Integer(section, "EvaluateTake", fallback: 10),
            UseAi = Boolean(section, "UseAi", fallback: true),
            SendDigests = Boolean(section, "SendDigests", fallback: true),
            TimeZoneId = Text(section, "TimeZoneId") ?? "America/Sao_Paulo"
        };
    }

    private static string? Text(IConfiguration section, string key)
    {
        var value = section[key];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int Integer(IConfiguration section, string key, int fallback)
        => int.TryParse(section[key], out var value) ? value : fallback;

    private static bool Boolean(IConfiguration section, string key, bool fallback)
        => bool.TryParse(section[key], out var value) ? value : fallback;
}
