namespace CareerHunter.Domain.SharedKernel;

/// <summary>
/// Base class for entities identified by a surrogate <see cref="Guid"/> key.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; }

    protected Entity()
    {
        Id = Guid.NewGuid();
    }

    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Entity id cannot be empty.");
        }

        Id = id;
    }

    public override bool Equals(object? obj)
        => obj is Entity other && other.GetType() == GetType() && other.Id == Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}

/// <summary>
/// Base class for aggregate roots. Timestamps are supplied by the caller so the
/// domain stays deterministic and testable (no ambient clock).
/// </summary>
public abstract class AggregateRoot : Entity
{
    public DateTimeOffset CreatedAt { get; protected set; }

    public DateTimeOffset UpdatedAt { get; protected set; }

    protected AggregateRoot()
    {
    }

    protected AggregateRoot(Guid id, DateTimeOffset createdAt)
        : base(id)
    {
        if (createdAt == default)
        {
            throw new DomainException("CreatedAt is required.");
        }

        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    protected void Touch(DateTimeOffset now)
    {
        if (now == default)
        {
            throw new DomainException("Timestamp is required.");
        }

        UpdatedAt = now;
    }
}
