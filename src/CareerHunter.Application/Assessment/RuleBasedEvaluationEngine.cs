using CareerHunter.Application.Catalog;
using CareerHunter.Domain.Assessment;
using CareerHunter.Domain.SharedKernel;

namespace CareerHunter.Application.Assessment;

/// <summary>
/// Deterministic, explainable evaluation used as the default engine and as the fallback when
/// the AI provider is unavailable or returns an invalid answer. Same inputs always produce the
/// same scores, which makes it fully testable.
/// </summary>
public sealed class RuleBasedEvaluationEngine : IJobEvaluationEngine
{
    private static readonly string[] ArchitectureKeywords =
    [
        "arquitetura", "architecture", "architect", "microservices", "microsservicos",
        "distributed", "distribuidos", "ddd", "lead", "lider", "design", "integration", "integracao"
    ];

    public Task<JobEvaluationDraft> EvaluateAsync(
        CandidateProfileSnapshot candidate,
        JobOpportunitySnapshot job,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(job);

        var strengths = new List<string>();
        var gaps = new List<string>();
        var assumptions = new List<string>();

        var matches = MatchRequirements(candidate, job, strengths, gaps);
        var technicalScore = ScoreRequirements(matches, job, assumptions);
        var architectureScore = ScoreArchitecture(matches, assumptions);
        var seniorityScore = ScoreSeniority(candidate, job, assumptions);
        var compensationScore = ScoreCompensation(candidate, job, assumptions);
        var modalityScore = ScoreModality(candidate, job, assumptions);
        var contractScore = ScoreContract(candidate, job, assumptions);
        var domainScore = ScoreBusinessDomain(candidate, job, assumptions);

        var dimensions = new List<EvaluationDimensionInput>
        {
            new(
                EvaluationDimension.Technical,
                technicalScore,
                0.40m,
                $"Cobertura das exigências técnicas declaradas: {matches.Count(match => match.Status == RequirementMatchStatus.Matched)} de {matches.Count} requisitos atendidos integralmente."),
            new(
                EvaluationDimension.Seniority,
                seniorityScore,
                0.15m,
                job.Seniority is null
                    ? "A vaga não declara senioridade; comparação com o alvo profissional ficou neutra."
                    : $"Senioridade da vaga ({job.Seniority}) comparada ao alvo profissional ({candidate.PrimaryTargetSeniority ?? "não declarado"})."),
            new(
                EvaluationDimension.ArchitectureAndLeadership,
                architectureScore,
                0.15m,
                "Experiência de arquitetura e liderança avaliada a partir dos requisitos e do histórico declarado."),
            new(
                EvaluationDimension.Compensation,
                compensationScore,
                0.12m,
                CompensationRationale(candidate, job)),
            new(
                EvaluationDimension.WorkModality,
                modalityScore,
                0.06m,
                job.Modality is null
                    ? "Modalidade não informada pela vaga; não penalizada."
                    : $"Modalidade da vaga ({job.Modality}) comparada às preferências declaradas."),
            new(
                EvaluationDimension.Contract,
                contractScore,
                0.05m,
                job.ContractType is null
                    ? "Tipo de contrato não informado pela vaga; não penalizado."
                    : $"Contrato da vaga ({job.ContractType}) comparado às preferências declaradas."),
            new(
                EvaluationDimension.BusinessDomain,
                domainScore,
                0.07m,
                "Afinidade de domínio de negócio estimada pelo histórico declarado e pela descrição da vaga.")
        };

        return Task.FromResult(new JobEvaluationDraft(
            dimensions,
            matches,
            strengths,
            gaps,
            assumptions,
            EvaluationEngine.RuleBased,
            null,
            null,
            null));
    }

    private static string CompensationRationale(CandidateProfileSnapshot candidate, JobOpportunitySnapshot job)
    {
        if (job.MinAmount is null && job.MaxAmount is null)
        {
            return "A vaga não publica faixa salarial; a remuneração não foi tratada como zero nem como incompatível.";
        }

        if (candidate.ExpectedMinAmount is null && candidate.ExpectedMaxAmount is null)
        {
            return "O candidato não declarou expectativa salarial; comparação neutra.";
        }

        return "Faixa da vaga comparada à expectativa declarada, convertida para base mensal.";
    }

    private static List<RequirementMatchInput> MatchRequirements(
        CandidateProfileSnapshot candidate,
        JobOpportunitySnapshot job,
        List<string> strengths,
        List<string> gaps)
    {
        var matches = new List<RequirementMatchInput>(job.Requirements.Count);
        var candidateSkills = candidate.Skills
            .Select(skill => (skill.Name, Tokens: TextNormalizer.Tokens(skill.Name), skill.Proficiency, skill.YearsOfExperience))
            .ToArray();
        var technologies = candidate.Experiences
            .SelectMany(experience => experience.Technologies)
            .Select(technology => TextNormalizer.ForMatching(technology))
            .Where(technology => technology.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        var certifications = candidate.Certifications
            .Select(certification => TextNormalizer.ForMatching(certification))
            .ToArray();

        foreach (var requirement in job.Requirements)
        {
            var normalizedRequirement = TextNormalizer.ForMatching(requirement.Name);
            var requirementTokens = TextNormalizer.Tokens(requirement.Name);
            string? matchedSkill = null;
            var bestOverlap = 0d;
            int? proficiency = null;
            decimal? years = null;

            foreach (var skill in candidateSkills)
            {
                var exactNameMatch = string.Equals(
                    normalizedRequirement,
                    TextNormalizer.ForMatching(skill.Name),
                    StringComparison.Ordinal);

                if (!exactNameMatch && (skill.Tokens.Count == 0 || requirementTokens.Count == 0))
                {
                    continue;
                }

                var overlap = requirementTokens.Count == 0
                    ? 0d
                    : requirementTokens.Intersect(skill.Tokens, StringComparer.Ordinal).Count()
                      / (double)requirementTokens.Count;

                // Exact normalized equality covers short names such as "C#" whose tokens are
                // filtered out. Phrase containment is only allowed for multi-token skills so a
                // single short token ("net" from ".NET") cannot match inside "kubernetes".
                if (exactNameMatch
                    || skill.Tokens.SetEquals(requirementTokens)
                    || (skill.Tokens.Count >= 2
                        && normalizedRequirement.Contains(string.Join(' ', skill.Tokens), StringComparison.Ordinal)))
                {
                    overlap = 1d;
                }

                if (overlap > bestOverlap)
                {
                    bestOverlap = overlap;
                    matchedSkill = skill.Name;
                    proficiency = skill.Proficiency;
                    years = skill.YearsOfExperience;
                }
            }

            var technologyEvidence = technologies.Contains(normalizedRequirement)
                                     || requirementTokens.Any(token => technologies.Contains(token));

            var certificationEvidence = certifications.Any(certification =>
                certification.Contains(normalizedRequirement, StringComparison.Ordinal));

            RequirementMatchStatus status;
            var evidence = new List<EvaluationEvidenceInput>();

            if (bestOverlap >= 0.999 || certificationEvidence)
            {
                status = RequirementMatchStatus.Matched;
                evidence.Add(new EvaluationEvidenceInput(
                    EvidenceKind.JobRequirement,
                    requirement.Name,
                    "job-requirement"));
                evidence.Add(new EvaluationEvidenceInput(
                    EvidenceKind.CandidateSkill,
                    $"Skill declarada '{matchedSkill ?? requirement.Name}' com proficiência {proficiency ?? 3}/5 e {years ?? 0} anos.",
                    matchedSkill ?? requirement.Name));
            }
            else if (bestOverlap >= 0.5 || technologyEvidence)
            {
                status = RequirementMatchStatus.PartiallyMatched;
                evidence.Add(new EvaluationEvidenceInput(
                    EvidenceKind.JobRequirement,
                    requirement.Name,
                    "job-requirement"));
                evidence.Add(new EvaluationEvidenceInput(
                    EvidenceKind.CandidateExperience,
                    technologyEvidence
                        ? "Tecnologia aparece no histórico de experiências, sem skill declarada equivalente."
                        : $"Correspondência parcial com a skill declarada '{matchedSkill}'.",
                    matchedSkill));
            }
            else
            {
                status = RequirementMatchStatus.Missing;
                evidence.Add(new EvaluationEvidenceInput(
                    EvidenceKind.MissingInformation,
                    $"Nenhuma skill, tecnologia ou certificação declarada corresponde a '{requirement.Name}'.",
                    requirement.Name));
            }

            matches.Add(new RequirementMatchInput(
                requirement.Name,
                requirement.IsRequired,
                status,
                status == RequirementMatchStatus.Missing ? "Requisito não evidenciado no perfil." : null,
                evidence));

            if (status == RequirementMatchStatus.Matched)
            {
                strengths.Add($"{requirement.Name}: evidência declarada no perfil.");
            }
            else if (status == RequirementMatchStatus.Missing)
            {
                gaps.Add(requirement.IsRequired
                    ? $"{requirement.Name}: requisito obrigatório sem evidência no perfil."
                    : $"{requirement.Name}: diferencial sem evidência no perfil (não elimina a oportunidade).");
            }
        }

        return matches;
    }

    private static int ScoreRequirements(
        IReadOnlyCollection<RequirementMatchInput> matches,
        JobOpportunitySnapshot job,
        List<string> assumptions)
    {
        if (matches.Count == 0)
        {
            assumptions.Add("A vaga não lista requisitos objetivos; a dimensão técnica ficou neutra (50).");
            return 50;
        }

        static decimal Points(RequirementMatchStatus status) => status switch
        {
            RequirementMatchStatus.Matched => 1m,
            RequirementMatchStatus.PartiallyMatched => 0.6m,
            RequirementMatchStatus.Unknown => 0.5m,
            _ => 0m
        };

        var required = matches.Where(match => match.IsRequired).ToArray();
        var optional = matches.Where(match => !match.IsRequired).ToArray();

        var missingRequired = required.Count(match => match.Status == RequirementMatchStatus.Missing);
        if (missingRequired > 0)
        {
            assumptions.Add($"{missingRequired} requisito(s) obrigatório(s) sem evidência; a nota técnica reflete essa lacuna.");
        }

        if (required.Length == 0)
        {
            assumptions.Add("Nenhum requisito obrigatório explícito; apenas diferenciais foram considerados.");
            return (int)Math.Round(optional.Average(match => Points(match.Status)) * 100, MidpointRounding.AwayFromZero);
        }

        // Required requirements dominate (80%); optional ones refine the score (20%).
        // Covering every required requirement therefore guarantees a high technical score,
        // which enforces the rule that a missing secondary technology cannot disqualify a job.
        var requiredScore = required.Average(match => Points(match.Status));
        var optionalScore = optional.Length == 0 ? requiredScore : optional.Average(match => Points(match.Status));
        var weighted = (requiredScore * 0.8m) + (optionalScore * 0.2m);

        return (int)Math.Round(weighted * 100, MidpointRounding.AwayFromZero);
    }

    private static int ScoreArchitecture(
        IReadOnlyCollection<RequirementMatchInput> matches,
        List<string> assumptions)
    {
        var architectureMatches = matches
            .Where(match => ArchitectureKeywords.Any(keyword =>
                TextNormalizer.ForMatching(match.RequirementName).Contains(keyword, StringComparison.Ordinal)))
            .ToArray();

        if (architectureMatches.Length == 0)
        {
            assumptions.Add("A vaga não exige explicitamente arquitetura ou liderança técnica; dimensão neutra (70).");
            return 70;
        }

        var score = architectureMatches.Average(match => match.Status switch
        {
            RequirementMatchStatus.Matched => 1d,
            RequirementMatchStatus.PartiallyMatched => 0.6d,
            RequirementMatchStatus.Unknown => 0.5d,
            _ => 0d
        });

        return (int)Math.Round(score * 100, MidpointRounding.AwayFromZero);
    }

    private static int ScoreSeniority(
        CandidateProfileSnapshot candidate,
        JobOpportunitySnapshot job,
        List<string> assumptions)
    {
        if (job.Seniority is null || !Enum.TryParse<SeniorityLevel>(job.Seniority, out var jobLevel))
        {
            assumptions.Add("Senioridade da vaga não informada; não tratada como incompatibilidade.");
            return 50;
        }

        if (candidate.PrimaryTargetSeniority is null
            || !Enum.TryParse<SeniorityLevel>(candidate.PrimaryTargetSeniority, out var candidateLevel))
        {
            assumptions.Add("Candidato não declarou senioridade em um alvo profissional; comparação neutra.");
            return 50;
        }

        var difference = (int)jobLevel - (int)candidateLevel;
        return difference switch
        {
            0 => 95,
            1 => 70,
            -1 => 80,
            2 => 45,
            -2 => 65,
            _ => 30
        };
    }

    private static int ScoreCompensation(
        CandidateProfileSnapshot candidate,
        JobOpportunitySnapshot job,
        List<string> assumptions)
    {
        if (job.MinAmount is null && job.MaxAmount is null)
        {
            assumptions.Add("Faixa salarial não publicada; remuneração não pontuada como falha.");
            return 50;
        }

        if (candidate.ExpectedMinAmount is null && candidate.ExpectedMaxAmount is null)
        {
            assumptions.Add("Expectativa salarial não declarada pelo candidato; comparação neutra.");
            return 50;
        }

        var candidateMin = candidate.ExpectedMinAmount ?? candidate.ExpectedMaxAmount!.Value;
        var candidateMax = candidate.ExpectedMaxAmount ?? candidateMin;
        var candidatePeriod = ParsePeriod(candidate.ExpectedPeriod);
        var jobPeriod = ParsePeriod(job.SalaryPeriod);

        var expectedMin = SalaryParser.ToMonthly(candidateMin, candidatePeriod);
        var expectedMax = SalaryParser.ToMonthly(candidateMax, candidatePeriod);
        var offerMin = SalaryParser.ToMonthly(job.MinAmount ?? job.MaxAmount!.Value, jobPeriod);
        var offerMax = SalaryParser.ToMonthly(job.MaxAmount ?? job.MinAmount!.Value, jobPeriod);

        if (offerMax < expectedMin * 0.9m)
        {
            return 20;
        }

        if (offerMax < expectedMin)
        {
            return 45;
        }

        if (offerMin > expectedMax * 1.2m)
        {
            return 85;
        }

        return 95;
    }

    private static int ScoreModality(
        CandidateProfileSnapshot candidate,
        JobOpportunitySnapshot job,
        List<string> assumptions)
    {
        if (job.Modality is null)
        {
            assumptions.Add("Modalidade não informada pela vaga; não penalizada.");
            return 50;
        }

        if (candidate.AcceptedModalities.Count == 0)
        {
            assumptions.Add("Candidato não declarou modalidades aceitas; comparação neutra.");
            return 50;
        }

        return candidate.AcceptedModalities.Contains(job.Modality, StringComparer.OrdinalIgnoreCase) ? 100 : 15;
    }

    private static int ScoreContract(
        CandidateProfileSnapshot candidate,
        JobOpportunitySnapshot job,
        List<string> assumptions)
    {
        if (job.ContractType is null)
        {
            assumptions.Add("Tipo de contrato não informado pela vaga; não penalizado.");
            return 50;
        }

        if (candidate.AcceptedContracts.Count == 0)
        {
            assumptions.Add("Candidato não declarou contratos aceitos; comparação neutra.");
            return 50;
        }

        return candidate.AcceptedContracts.Contains(job.ContractType, StringComparer.OrdinalIgnoreCase) ? 100 : 15;
    }

    private static int ScoreBusinessDomain(
        CandidateProfileSnapshot candidate,
        JobOpportunitySnapshot job,
        List<string> assumptions)
    {
        if (candidate.Experiences.Count == 0)
        {
            assumptions.Add("Nenhuma experiência profissional declarada; domínio de negócio não avaliado.");
            return 50;
        }

        var jobTokens = TextNormalizer.Tokens(job.Description);
        if (jobTokens.Count == 0)
        {
            return 55;
        }

        var candidateTokens = candidate.Experiences
            .SelectMany(experience => TextNormalizer.Tokens(experience.Description)
                .Concat(TextNormalizer.Tokens(experience.Title)))
            .ToHashSet(StringComparer.Ordinal);

        if (candidate.Summary is not null)
        {
            candidateTokens.UnionWith(TextNormalizer.Tokens(candidate.Summary));
        }

        var overlap = jobTokens.Intersect(candidateTokens, StringComparer.Ordinal).Count();
        var baseline = 55;
        var bonus = Math.Min(20, overlap);

        return Math.Clamp(baseline + bonus, 0, 85);
    }

    private static SalaryPeriod ParsePeriod(string? period)
        => Enum.TryParse<SalaryPeriod>(period, out var parsed) ? parsed : SalaryPeriod.Monthly;
}
