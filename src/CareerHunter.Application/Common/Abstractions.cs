namespace CareerHunter.Application.Common;

/// <summary>
/// Ambient time provider. Injected so use cases and tests are deterministic.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
}

/// <summary>
/// Transaction boundary. One use case equals one unit of work.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed class NotFoundException : Exception
{
    public NotFoundException(string resource, object key)
        : base($"{resource} '{key}' was not found.")
    {
        Resource = resource;
        Key = key;
    }

    public string Resource { get; }

    public object Key { get; }
}

public sealed class ConflictException : Exception
{
    public ConflictException(string message)
        : base(message)
    {
    }

    public ConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Requested operation is not valid in the current state (HTTP 409/422 semantics).
/// </summary>
public sealed class BusinessRuleException : Exception
{
    public BusinessRuleException(string message)
        : base(message)
    {
    }
}
