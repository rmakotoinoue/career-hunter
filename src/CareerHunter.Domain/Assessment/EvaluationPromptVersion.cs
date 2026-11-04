using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Domain.Assessment;

/// <summary>
/// Versioned prompt template. Keeping it in the database makes every evaluation traceable
/// to the exact instructions that produced it.
/// </summary>
public sealed class EvaluationPromptVersion : AggregateRoot
{
    private EvaluationPromptVersion()
    {
        Version = string.Empty;
        Template = string.Empty;
    }

    private EvaluationPromptVersion(string version, string template, bool isActive, string? notes, DateTimeOffset now)
        : base(Guid.NewGuid(), now)
    {
        Version = Guard.NotBlank(version, "version", 40);
        Template = Guard.NotBlank(template, "template", 20_000, 50);
        Notes = Guard.Optional(notes, "notes", 1000);
        IsActive = isActive;

        foreach (var placeholder in RequiredPlaceholders)
        {
            if (!Template.Contains(placeholder, StringComparison.Ordinal))
            {
                throw new DomainValidationException("template", $"Template must contain the {placeholder} placeholder.");
            }
        }
    }

    public static readonly string[] RequiredPlaceholders = ["{{candidate}}", "{{job}}", "{{schema}}"];

    public string Version { get; private set; }

    public string Template { get; private set; }

    public bool IsActive { get; private set; }

    public string? Notes { get; private set; }

    public static EvaluationPromptVersion Create(string version, string template, bool isActive, string? notes, DateTimeOffset now)
        => new(version, template, isActive, notes, now);

    public void Activate(DateTimeOffset now)
    {
        IsActive = true;
        Touch(now);
    }

    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        Touch(now);
    }
}
