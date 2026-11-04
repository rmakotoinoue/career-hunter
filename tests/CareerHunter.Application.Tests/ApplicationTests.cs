using CareerHunter.Application.Assessment;
using CareerHunter.Application.Catalog;
using CareerHunter.Domain.Assessment;
using CareerHunter.Domain.SharedKernel;
using Xunit;

namespace CareerHunter.Application.Tests;

public sealed class SalaryParserTests
{
    [Fact]
    public void Parses_explicit_brl_range()
    {
        var parsed = SalaryParser.TryParse("Faixa: R$ 9.000 - R$ 12.000 por mês", out var reason);

        Assert.Null(reason);
        Assert.NotNull(parsed);
        Assert.Equal(9000m, parsed!.MinAmount);
        Assert.Equal(12000m, parsed.MaxAmount);
        Assert.Equal("BRL", parsed.Currency);
        Assert.Equal(SalaryPeriod.Monthly, parsed.Period);
    }

    [Fact]
    public void Missing_salary_stays_unknown_never_zero()
    {
        var parsed = SalaryParser.TryParse("Remuneração a combinar.", out var reason);

        Assert.Null(parsed);
        Assert.Null(reason);
    }

    [Fact]
    public void Rejects_inverted_range()
    {
        var parsed = SalaryParser.TryParse("R$ 12.000 - R$ 9.000", out var reason);

        Assert.Null(parsed);
        Assert.NotNull(reason);
    }
}

public sealed class JobRequirementExtractorTests
{
    private readonly JobRequirementExtractor _extractor = new();

    [Fact]
    public void Classifies_required_and_preferred_sections()
    {
        const string description = """
            Requisitos:
            .NET e AWS
            Diferenciais:
            Kubernetes
            """;

        var requirements = _extractor.Extract(description);

        Assert.Contains(requirements, requirement => requirement.Name == ".NET" && requirement.IsRequired);
        Assert.Contains(requirements, requirement => requirement.Name == "AWS" && requirement.IsRequired);
        Assert.Contains(requirements, requirement => requirement.Name == "Kubernetes" && !requirement.IsRequired);
    }

    [Fact]
    public void Empty_description_returns_no_requirements()
    {
        Assert.Empty(_extractor.Extract(null));
        Assert.Empty(_extractor.Extract("   "));
    }
}

public sealed class RuleBasedEvaluationEngineTests
{
    private readonly RuleBasedEvaluationEngine _engine = new();

    [Fact]
    public async Task Missing_secondary_technology_does_not_disqualify()
    {
        var candidate = new CandidateProfileSnapshot(
            Guid.NewGuid(),
            "Rafael Makoto Inoue",
            "Engenheiro de software sênior",
            "Modernização de sistemas .NET e integração de serviços.",
            15,
            [
                new CandidateSkillSnapshot(".NET", "Technical", 5, 15),
                new CandidateSkillSnapshot("AWS", "Technical", 4, 6)
            ],
            [
                new CandidateExperienceSnapshot(
                    "Empresa Exemplo",
                    "Tech Lead",
                    new DateOnly(2018, 1, 1),
                    null,
                    "Liderança de modernização .NET e integração de serviços.",
                    [".NET", "AWS"])
            ],
            [],
            "Engenheiro de Software",
            "Senior",
            ["Remote"],
            ["Clt", "Pj"],
            9000,
            12000,
            "BRL",
            "Monthly");

        var job = new JobOpportunitySnapshot(
            Guid.NewGuid(),
            "Engenheiro .NET Sênior",
            "Empresa Exemplo",
            "Remoto",
            "Remote",
            "Clt",
            "Senior",
            10000,
            13000,
            "BRL",
            "Monthly",
            "Modernização de sistemas .NET com integração de serviços.",
            null,
            [
                new JobRequirementSnapshot(".NET", "Technology", true),
                new JobRequirementSnapshot("AWS", "Technology", true),
                new JobRequirementSnapshot("Kubernetes", "Technology", false)
            ]);

        var draft = await _engine.EvaluateAsync(candidate, job);

        var technical = draft.Dimensions.Single(dimension => dimension.Dimension == EvaluationDimension.Technical);
        Assert.Equal(80, technical.Score);
        Assert.Equal(EvaluationEngine.RuleBased, draft.Engine);

        var kubernetes = draft.RequirementMatches.Single(match => match.RequirementName == "Kubernetes");
        Assert.Equal(RequirementMatchStatus.Missing, kubernetes.Status);
        Assert.Contains(draft.Gaps, gap => gap.Contains("Kubernetes", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class EvaluationPromptTests
{
    private static readonly JobOpportunitySnapshot Job = new(
        Guid.NewGuid(),
        "Engenheiro .NET",
        "Empresa Exemplo",
        "Remoto",
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        "Descrição da vaga.",
        null,
        [new JobRequirementSnapshot(".NET", "Technology", true)]);

    [Fact]
    public void Prompt_replacement_is_single_pass()
    {
        var builder = new EvaluationPromptBuilder();
        var request = new JobEvaluationRequest(
            CandidateSnapshotJson: "candidato com {{job}} literal",
            JobSnapshotJson: "{\"title\":\"vaga\"}",
            OutputSchemaJson: "{\"type\":\"object\"}",
            PromptVersion: "v1");

        var prompt = builder.BuildUserPrompt("C={{candidate}};J={{job}};S={{schema}}", request);

        Assert.Contains("candidato com {{job}} literal", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("C=candidato com {\"title\":\"vaga\"}", prompt, StringComparison.Ordinal);
        Assert.EndsWith("S={\"type\":\"object\"}", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Validator_accepts_well_formed_payload()
    {
        var validator = new JobEvaluationResultValidator();
        var payload = ValidPayload();

        var draft = validator.Validate(payload, Job, "openai", "gpt-4o-mini", "v1");

        Assert.Equal(EvaluationEngine.Ai, draft.Engine);
        Assert.Equal("openai", draft.ModelProvider);
        Assert.Equal("v1", draft.PromptVersion);
    }

    [Fact]
    public void Validator_rejects_omitted_required_requirement()
    {
        var validator = new JobEvaluationResultValidator();
        var payload = ValidPayload() with
        {
            RequirementMatches =
            [
                new AiRequirementMatchPayload("Kubernetes", "Missing", null,
                    [new AiEvidencePayload("MissingInformation", "Não declarado.", null)])
            ]
        };

        Assert.Throws<AiEvaluationValidationException>(
            () => validator.Validate(payload, Job, "openai", "gpt-4o-mini", "v1"));
    }

    [Fact]
    public void Validator_rejects_unknown_dimension()
    {
        var validator = new JobEvaluationResultValidator();
        var payload = ValidPayload() with
        {
            Dimensions =
            [
                new AiDimensionPayload("Technical", 90, 0.4m, "Evidência declarada no perfil do candidato."),
                new AiDimensionPayload("NotADimension", 80, 0.2m, "Evidência declarada no perfil do candidato."),
                new AiDimensionPayload("Seniority", 90, 0.4m, "Evidência declarada no perfil do candidato.")
            ]
        };

        Assert.Throws<AiEvaluationValidationException>(
            () => validator.Validate(payload, Job, "openai", "gpt-4o-mini", "v1"));
    }

    private static AiEvaluationPayload ValidPayload() => new(
        [
            new AiDimensionPayload("Technical", 90, 0.40m, "Requisito obrigatório coberto por skill declarada."),
            new AiDimensionPayload("Seniority", 90, 0.15m, "Senioridade compatível com o alvo declarado."),
            new AiDimensionPayload("ArchitectureAndLeadership", 80, 0.15m, "Experiência de arquitetura declarada."),
            new AiDimensionPayload("Compensation", 60, 0.12m, "Faixa salarial comparada à expectativa declarada."),
            new AiDimensionPayload("WorkModality", 50, 0.06m, "Modalidade não informada pela vaga."),
            new AiDimensionPayload("Contract", 50, 0.05m, "Contrato não informado pela vaga."),
            new AiDimensionPayload("BusinessDomain", 60, 0.07m, "Domínio parcialmente aderente ao histórico.")
        ],
        [
            new AiRequirementMatchPayload(".NET", "Matched", null,
            [
                new AiEvidencePayload("CandidateSkill", "Skill .NET declarada com 15 anos.", ".NET")
            ])
        ],
        ["Requisito obrigatório coberto."],
        ["Kubernetes não declarado (diferencial)."],
        ["Faixa salarial não publicada."]);
}
