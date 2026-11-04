using CareerHunter.Domain.Assessment;
using CareerHunter.Domain.Candidates;
using CareerHunter.Domain.SharedKernel;
using Xunit;

namespace CareerHunter.Domain.Tests;

public sealed class CandidateProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_keeps_unknown_salary_null()
    {
        var profile = CreateProfile();

        Assert.Null(profile.CompensationExpectation);
        Assert.Null(profile.WorkPreferences);
        Assert.Equal(15, profile.YearsOfExperience);
    }

    [Fact]
    public void AddSkill_rejects_duplicate_normalized_name()
    {
        var profile = CreateProfile();
        profile.AddSkill(".NET", SkillCategory.Technical, 5, 15, Now);

        var exception = Assert.Throws<DomainException>(
            () => profile.AddSkill(".net", SkillCategory.Technical, 4, 10, Now));

        Assert.Contains(".net", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkPreferences_require_at_least_one_modality_and_contract()
    {
        Assert.Throws<DomainValidationException>(() =>
            WorkPreferences.Create(
                acceptsRemote: false,
                acceptsHybrid: false,
                acceptsOnsite: false,
                acceptsClt: true,
                acceptsPj: false));

        Assert.Throws<DomainValidationException>(() =>
            WorkPreferences.Create(
                acceptsRemote: true,
                acceptsHybrid: false,
                acceptsOnsite: false,
                acceptsClt: false,
                acceptsPj: false));
    }

    [Fact]
    public void SalaryExpectation_is_preserved_with_period_and_currency()
    {
        var profile = CreateProfile();
        profile.UpdateCompensationExpectation(
            SalaryRange.Create(9000, 12000, SalaryPeriod.Monthly, "BRL"),
            Now);

        Assert.NotNull(profile.CompensationExpectation);
        Assert.Equal(9000, profile.CompensationExpectation!.MinAmount);
        Assert.Equal(12000, profile.CompensationExpectation.MaxAmount);
        Assert.Equal(SalaryPeriod.Monthly, profile.CompensationExpectation.Period);
    }

    internal static CandidateProfile CreateProfile()
        => CandidateProfile.Create(
            "Rafael Makoto Inoue",
            EmailAddress.Create("rafael.inoue@example.com"),
            "Engenheiro de software sênior",
            "Atuação em modernização de sistemas .NET e integração de serviços.",
            CandidateLocation.Create("São Paulo", "SP", "Brasil"),
            yearsOfExperience: 15,
            linkedInUrl: null,
            Now);
}

public sealed class JobEvaluationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid CandidateId = Guid.NewGuid();
    private static readonly Guid JobId = Guid.NewGuid();

    [Fact]
    public void Overall_score_is_recomputed_from_weighted_dimensions()
    {
        var evaluation = CreateEvaluation(technicalScore: 90, requiredMatched: true);

        Assert.Equal(84, evaluation.OverallScore);
        Assert.Equal(EvaluationRecommendation.Match, evaluation.Recommendation);
    }

    [Fact]
    public void Missing_secondary_requirement_cannot_disqualify_when_required_are_matched()
    {
        var evaluation = CreateEvaluation(technicalScore: 90, requiredMatched: true);

        var secondary = evaluation.RequirementMatches.Single(match => !match.IsRequired);
        Assert.Equal(RequirementMatchStatus.Missing, secondary.Status);
        Assert.True(evaluation.OverallScore >= 55);
    }

    [Fact]
    public void Policy_rejects_low_technical_score_when_required_requirements_are_covered()
    {
        Assert.Throws<DomainException>(() => CreateEvaluation(technicalScore: 40, requiredMatched: true));
    }

    [Fact]
    public void Ai_evaluation_requires_provider_model_and_prompt_version()
    {
        Assert.Throws<DomainValidationException>(() => JobEvaluation.Create(
            CandidateId,
            JobId,
            EvaluationEngine.Ai,
            Dimensions(80),
            Matches(requiredMatched: true),
            [],
            [],
            [],
            modelProvider: null,
            modelName: "gpt-4o-mini",
            promptVersion: "v1",
            inputFingerprint: "0123456789abcdef",
            Now));
    }

    private static JobEvaluation CreateEvaluation(int technicalScore, bool requiredMatched)
        => JobEvaluation.Create(
            CandidateId,
            JobId,
            EvaluationEngine.RuleBased,
            Dimensions(technicalScore),
            Matches(requiredMatched),
            ["Requisito obrigatório coberto por skill declarada."],
            ["Requisito secundário sem evidência no perfil."],
            ["Faixa salarial não publicada."],
            null,
            null,
            null,
            "0123456789abcdef",
            Now);

    private static IReadOnlyCollection<EvaluationDimensionInput> Dimensions(int technicalScore) =>
    [
        new(EvaluationDimension.Technical, technicalScore, 0.40m, "Cobertura dos requisitos técnicos."),
        new(EvaluationDimension.Seniority, 95, 0.15m, "Senioridade compatível."),
        new(EvaluationDimension.ArchitectureAndLeadership, 80, 0.15m, "Experiência de arquitetura declarada."),
        new(EvaluationDimension.Compensation, 50, 0.12m, "Faixa salarial não publicada."),
        new(EvaluationDimension.WorkModality, 100, 0.06m, "Modalidade remota aceita."),
        new(EvaluationDimension.Contract, 100, 0.05m, "Contrato PJ aceito."),
        new(EvaluationDimension.BusinessDomain, 70, 0.07m, "Domínio de negócio parcialmente aderente.")
    ];

    private static IReadOnlyCollection<RequirementMatchInput> Matches(bool requiredMatched) =>
    [
        new(
            ".NET",
            IsRequired: true,
            requiredMatched ? RequirementMatchStatus.Matched : RequirementMatchStatus.Missing,
            null,
            [new EvaluationEvidenceInput(EvidenceKind.CandidateSkill, "Skill declarada .NET.", ".NET")]),
        new(
            "Kubernetes",
            IsRequired: false,
            RequirementMatchStatus.Missing,
            "Diferencial sem evidência.",
            [new EvaluationEvidenceInput(EvidenceKind.MissingInformation, "Não declarado.")])
    ];
}

public sealed class SharedKernelTests
{
    [Fact]
    public void Money_rejects_negative_amounts()
    {
        Assert.Throws<DomainValidationException>(() => Money.Create(-1));
    }

    [Fact]
    public void Money_rounds_half_to_even_and_keeps_currency()
    {
        var money = Money.Create(10.005m, "BRL");

        Assert.Equal(10.00m, money.Amount);
        Assert.Equal("BRL", money.Currency);
    }

    [Fact]
    public void SalaryRange_rejects_inverted_interval()
    {
        Assert.Throws<DomainValidationException>(
            () => SalaryRange.Create(12000, 9000, SalaryPeriod.Monthly, "BRL"));
    }

    [Fact]
    public void EmailAddress_rejects_invalid_input()
    {
        Assert.Throws<DomainValidationException>(() => EmailAddress.Create("not-an-email"));
    }
}

public sealed class JobSearchPlanTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Plan_uses_the_primary_target_and_the_strongest_technical_skills()
    {
        var profile = CandidateProfileTests.CreateProfile();
        profile.AddCareerTarget("Tech Lead", SeniorityLevel.Lead, isPrimary: true, notes: null, Now);
        profile.AddCareerTarget("Desenvolvedor Backend", SeniorityLevel.Senior, isPrimary: false, notes: null, Now);
        profile.AddSkill(".NET", SkillCategory.Technical, 5, 15, Now);
        profile.AddSkill("C#", SkillCategory.Technical, 5, 15, Now);
        profile.AddSkill("Azure", SkillCategory.Technical, 4, 6, Now);
        profile.AddSkill("Liderança técnica", SkillCategory.Behavioral, 5, 8, Now);
        profile.UpdateWorkPreferences(WorkPreferences.Create(true, true, false, true, true), Now);

        var plan = JobSearchPlan.FromProfile(profile);

        Assert.Equal("Tech Lead", plan.TitleQuery);
        Assert.Equal(".NET C#", plan.SkillQuery);
        Assert.Equal("Tech Lead .NET C#", plan.Query);
        Assert.Contains("tech lead", plan.Keywords);
        Assert.Contains("backend", plan.Keywords);
        Assert.Contains(".net", plan.Keywords);
        Assert.DoesNotContain("lideranca técnica", plan.Keywords);
        Assert.DoesNotContain("sênior", plan.Keywords);
        Assert.False(plan.IsEmpty);
    }

    [Fact]
    public void Plan_searches_the_whole_country_when_remote_is_accepted()
    {
        var profile = CandidateProfileTests.CreateProfile();
        profile.AddCareerTarget("Tech Lead", SeniorityLevel.Lead, isPrimary: true, notes: null, Now);
        profile.UpdateWorkPreferences(WorkPreferences.Create(true, false, false, true, true), Now);

        var plan = JobSearchPlan.FromProfile(profile);

        Assert.Equal("Brasil", plan.ProviderLocation);
        Assert.Null(plan.GeographicFilter);
    }

    [Fact]
    public void Plan_filters_by_the_declared_city_when_remote_is_not_accepted()
    {
        var profile = CandidateProfileTests.CreateProfile();
        profile.AddCareerTarget("Tech Lead", SeniorityLevel.Lead, isPrimary: true, notes: null, Now);
        profile.UpdateWorkPreferences(WorkPreferences.Create(false, true, true, true, true), Now);

        var plan = JobSearchPlan.FromProfile(profile);

        Assert.Equal("São Paulo, SP", plan.ProviderLocation);
        Assert.Equal("São Paulo, SP", plan.GeographicFilter);
    }

    [Fact]
    public void Plan_falls_back_to_the_headline_when_no_career_target_exists()
    {
        var profile = CandidateProfileTests.CreateProfile();

        var plan = JobSearchPlan.FromProfile(profile);

        Assert.Contains("software", plan.Keywords);
        Assert.Contains("engenheiro", plan.Keywords);
        Assert.DoesNotContain("sênior", plan.Keywords);
        Assert.StartsWith("Engenheiro de software", plan.Query);
    }
}
