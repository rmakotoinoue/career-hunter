namespace CareerHunter.Domain.SharedKernel;

/// <summary>
/// Raised when a business invariant is violated.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Raised when input cannot be turned into a valid domain object.
/// Carries field-level errors so the API can build a Problem Details response.
/// </summary>
public sealed class DomainValidationException : DomainException
{
    public DomainValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public DomainValidationException(string field, string error)
        : this(new Dictionary<string, string[]> { [field] = [error] })
    {
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
