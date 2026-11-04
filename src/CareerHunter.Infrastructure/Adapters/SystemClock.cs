using CareerHunter.Application.Common;

namespace CareerHunter.Infrastructure.Adapters;

/// <summary>
/// UTC clock. Tests never use this implementation; they inject a fake.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
