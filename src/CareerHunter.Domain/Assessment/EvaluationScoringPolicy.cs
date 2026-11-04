using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Domain.Assessment;

public enum EvaluationEngine
{
    RuleBased = 1,
    Ai = 2
}

public enum EvaluationDimension
{
    Technical = 1,
    Seniority = 2,
    ArchitectureAndLeadership = 3,
    Compensation = 4,
    WorkModality = 5,
    Contract = 6,
    BusinessDomain = 7
}

public enum RequirementMatchStatus
{
    Matched = 1,
    PartiallyMatched = 2,
    Missing = 3,
    Unknown = 4
}

public enum EvaluationRecommendation
{
    StrongMatch = 1,
    Match = 2,
    PartialMatch = 3,
    WeakMatch = 4,
    NoMatch = 5
}

public enum EvidenceKind
{
    JobRequirement = 1,
    CandidateSkill = 2,
    CandidateExperience = 3,
    CandidateCertification = 4,
    MissingInformation = 5
}

public sealed record EvaluationDimensionInput(
    EvaluationDimension Dimension,
    int Score,
    decimal Weight,
    string Rationale);

public sealed record EvaluationEvidenceInput(
    EvidenceKind Kind,
    string Text,
    string? Reference = null);

public sealed record RequirementMatchInput(
    string RequirementName,
    bool IsRequired,
    RequirementMatchStatus Status,
    string? Note = null,
    IReadOnlyCollection<EvaluationEvidenceInput>? Evidence = null);

/// <summary>
/// Deterministic scoring rules. Used by the rule-based engine, by the AI result validator
/// and by the aggregate itself so an AI answer can never bypass the business policy.
/// </summary>
public static class EvaluationScoringPolicy
{
    public const decimal WeightTolerance = 0.001m;
    public const int MinimumTechnicalScoreWithOnlySecondaryGaps = 50;

    public static int ComputeOverallScore(IEnumerable<EvaluationDimensionInput> dimensions)
    {
        ArgumentNullException.ThrowIfNull(dimensions);
        var weighted = dimensions.Sum(dimension => dimension.Score * dimension.Weight);
        return (int)Math.Round(weighted, MidpointRounding.AwayFromZero);
    }

    public static EvaluationRecommendation Recommend(int overallScore) => overallScore switch
    {
        >= 85 => EvaluationRecommendation.StrongMatch,
        >= 70 => EvaluationRecommendation.Match,
        >= 55 => EvaluationRecommendation.PartialMatch,
        >= 40 => EvaluationRecommendation.WeakMatch,
        _ => EvaluationRecommendation.NoMatch
    };

    public static void ValidateDimensions(IReadOnlyCollection<EvaluationDimensionInput> dimensions)
    {
        if (dimensions.Count == 0)
        {
            throw new DomainValidationException("dimensions", "At least one evaluation dimension is required.");
        }

        if (dimensions.Select(dimension => dimension.Dimension).Distinct().Count() != dimensions.Count)
        {
            throw new DomainValidationException("dimensions", "Evaluation dimensions cannot be duplicated.");
        }

        foreach (var dimension in dimensions)
        {
            if (!Enum.IsDefined(dimension.Dimension))
            {
                throw new DomainValidationException("dimensions", "Unknown evaluation dimension.");
            }

            if (dimension.Score is < 0 or > 100)
            {
                throw new DomainValidationException("dimensions", "Dimension scores must be between 0 and 100.");
            }

            if (dimension.Weight <= 0 || dimension.Weight > 1)
            {
                throw new DomainValidationException("dimensions", "Dimension weights must be greater than 0 and at most 1.");
            }

            if (string.IsNullOrWhiteSpace(dimension.Rationale))
            {
                throw new DomainValidationException("dimensions", "Every dimension requires a rationale.");
            }
        }

        var totalWeight = dimensions.Sum(dimension => dimension.Weight);
        if (Math.Abs(totalWeight - 1m) > WeightTolerance)
        {
            throw new DomainValidationException("dimensions", "Dimension weights must sum to 1.0.");
        }
    }

    /// <summary>
    /// Business rule: a missing secondary technology can lower the score, but it can never
    /// disqualify the opportunity. When every required requirement is covered, the technical
    /// score must stay above the floor.
    /// </summary>
    public static void EnsureNotDisqualifiedBySecondaryGaps(
        int technicalScore,
        IReadOnlyCollection<RequirementMatchInput> matches)
    {
        if (matches.Count == 0)
        {
            return;
        }

        var requiredMatches = matches.Where(match => match.IsRequired).ToArray();
        var requiredCovered = requiredMatches.Length > 0
                              && requiredMatches.All(match => match.Status is RequirementMatchStatus.Matched or RequirementMatchStatus.PartiallyMatched);

        if (requiredCovered && technicalScore < MinimumTechnicalScoreWithOnlySecondaryGaps)
        {
            throw new DomainException(
                "A missing secondary requirement cannot disqualify an opportunity whose required requirements are covered.");
        }

        var anyMatched = matches.Any(match => match.Status is RequirementMatchStatus.Matched or RequirementMatchStatus.PartiallyMatched);
        if (technicalScore == 0 && anyMatched)
        {
            throw new DomainException("A technical score of zero is not allowed when requirements are matched.");
        }
    }

    public static void ValidateMatches(IReadOnlyCollection<RequirementMatchInput> matches)
    {
        if (matches.Count == 0)
        {
            return;
        }

        var duplicated = matches
            .GroupBy(match => TextNormalizer.ForMatching(match.RequirementName), StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicated is not null)
        {
            throw new DomainValidationException("requirementMatches", "Requirement matches cannot be duplicated.");
        }

        foreach (var match in matches)
        {
            Guard.NotBlank(match.RequirementName, "requirementName", 200);

            if (!Enum.IsDefined(match.Status))
            {
                throw new DomainValidationException("requirementMatches", "Unknown requirement match status.");
            }

            if (match.Status == RequirementMatchStatus.Missing && match.Evidence is { Count: > 0 }
                && match.Evidence.Any(evidence => evidence.Kind != EvidenceKind.MissingInformation))
            {
                throw new DomainValidationException(
                    "requirementMatches",
                    "A missing requirement can only reference missing-information evidence.");
            }
        }
    }
}
