using CareerHunter.Application.Abstractions;
using CareerHunter.Domain.Candidates;
using CareerHunter.Domain.Discovery;

namespace CareerHunter.Infrastructure.Adapters;

/// <summary>
/// Manual sources are fed through the API instead of a remote endpoint. Collecting one is a
/// successful no-op so the discovery pipeline never reports a missing adapter.
/// </summary>
public sealed class ManualJobSourceAdapter : IJobSourceAdapter
{
    public JobSourceKind Kind => JobSourceKind.Manual;

    public Task<IReadOnlyList<CollectedJob>> CollectAsync(
        JobSource source,
        JobSearchPlan? searchPlan = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CollectedJob>>([]);
}
