using CareerHunter.Domain.Assessment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerHunter.Infrastructure.Persistence.Configurations;

public sealed class JobEvaluationConfiguration : IEntityTypeConfiguration<JobEvaluation>
{
    public void Configure(EntityTypeBuilder<JobEvaluation> builder)
    {
        builder.ToTable("job_evaluations");
        builder.HasKey(evaluation => evaluation.Id);
        builder.Property(evaluation => evaluation.Id).HasColumnName("id");
        builder.Property(evaluation => evaluation.CandidateProfileId).HasColumnName("candidate_profile_id");
        builder.Property(evaluation => evaluation.JobOpportunityId).HasColumnName("job_opportunity_id");
        builder.Property(evaluation => evaluation.Engine).HasColumnName("engine").HasConversion<string>().HasMaxLength(20);
        builder.Property(evaluation => evaluation.OverallScore).HasColumnName("overall_score");
        builder.Property(evaluation => evaluation.Recommendation).HasColumnName("recommendation").HasConversion<string>().HasMaxLength(20);
        builder.Property(evaluation => evaluation.EvaluatedAt).HasColumnName("evaluated_at");
        builder.Property(evaluation => evaluation.ModelProvider).HasColumnName("model_provider").HasMaxLength(80);
        builder.Property(evaluation => evaluation.ModelName).HasColumnName("model_name").HasMaxLength(120);
        builder.Property(evaluation => evaluation.PromptVersion).HasColumnName("prompt_version").HasMaxLength(40);
        builder.Property(evaluation => evaluation.InputFingerprint).HasColumnName("input_fingerprint").HasMaxLength(128).IsRequired();
        builder.Property(evaluation => evaluation.CreatedAt).HasColumnName("created_at");
        builder.Property(evaluation => evaluation.UpdatedAt).HasColumnName("updated_at");

        builder.Property(evaluation => evaluation.Strengths).HasJsonListConversion("strengths");
        builder.Property(evaluation => evaluation.Gaps).HasJsonListConversion("gaps");
        builder.Property(evaluation => evaluation.Assumptions).HasJsonListConversion("assumptions");

        builder.HasMany(evaluation => evaluation.Dimensions)
            .WithOne()
            .HasForeignKey(dimension => dimension.JobEvaluationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(evaluation => evaluation.RequirementMatches)
            .WithOne()
            .HasForeignKey(match => match.JobEvaluationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(evaluation => evaluation.Dimensions).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(evaluation => evaluation.RequirementMatches).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(evaluation => new { evaluation.JobOpportunityId, evaluation.EvaluatedAt });
        builder.HasIndex(evaluation => new { evaluation.CandidateProfileId, evaluation.OverallScore });
        builder.HasIndex(evaluation => new { evaluation.CandidateProfileId, evaluation.JobOpportunityId, evaluation.EvaluatedAt });
    }
}

public sealed class EvaluationDimensionScoreConfiguration : IEntityTypeConfiguration<EvaluationDimensionScore>
{
    public void Configure(EntityTypeBuilder<EvaluationDimensionScore> builder)
    {
        builder.ToTable("evaluation_dimension_scores");
        builder.HasKey(dimension => dimension.Id);
        builder.Property(dimension => dimension.Id).HasColumnName("id");
        builder.Property(dimension => dimension.JobEvaluationId).HasColumnName("job_evaluation_id");
        builder.Property(dimension => dimension.Dimension).HasColumnName("dimension").HasConversion<string>().HasMaxLength(40);
        builder.Property(dimension => dimension.Score).HasColumnName("score");
        builder.Property(dimension => dimension.Weight).HasColumnName("weight").HasPrecision(5, 4);
        builder.Property(dimension => dimension.Rationale).HasColumnName("rationale").HasMaxLength(1000).IsRequired();
        builder.HasIndex(dimension => new { dimension.JobEvaluationId, dimension.Dimension }).IsUnique();
    }
}

public sealed class RequirementMatchConfiguration : IEntityTypeConfiguration<RequirementMatch>
{
    public void Configure(EntityTypeBuilder<RequirementMatch> builder)
    {
        builder.ToTable("requirement_matches");
        builder.HasKey(match => match.Id);
        builder.Property(match => match.Id).HasColumnName("id");
        builder.Property(match => match.JobEvaluationId).HasColumnName("job_evaluation_id");
        builder.Property(match => match.RequirementName).HasColumnName("requirement_name").HasMaxLength(200).IsRequired();
        builder.Property(match => match.NormalizedRequirementName).HasColumnName("normalized_requirement_name").HasMaxLength(200).IsRequired();
        builder.Property(match => match.IsRequired).HasColumnName("is_required");
        builder.Property(match => match.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        builder.Property(match => match.Note).HasColumnName("note").HasMaxLength(1000);

        builder.HasMany(match => match.Evidence)
            .WithOne()
            .HasForeignKey(evidence => evidence.RequirementMatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(match => match.Evidence).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(match => new { match.JobEvaluationId, match.NormalizedRequirementName }).IsUnique();
    }
}

public sealed class EvaluationEvidenceConfiguration : IEntityTypeConfiguration<EvaluationEvidence>
{
    public void Configure(EntityTypeBuilder<EvaluationEvidence> builder)
    {
        builder.ToTable("evaluation_evidences");
        builder.HasKey(evidence => evidence.Id);
        builder.Property(evidence => evidence.Id).HasColumnName("id");
        builder.Property(evidence => evidence.RequirementMatchId).HasColumnName("requirement_match_id");
        builder.Property(evidence => evidence.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(40);
        builder.Property(evidence => evidence.Text).HasColumnName("text").HasMaxLength(2000).IsRequired();
        builder.Property(evidence => evidence.Reference).HasColumnName("reference").HasMaxLength(200);
    }
}

public sealed class EvaluationPromptVersionConfiguration : IEntityTypeConfiguration<EvaluationPromptVersion>
{
    public void Configure(EntityTypeBuilder<EvaluationPromptVersion> builder)
    {
        builder.ToTable("evaluation_prompt_versions");
        builder.HasKey(prompt => prompt.Id);
        builder.Property(prompt => prompt.Id).HasColumnName("id");
        builder.Property(prompt => prompt.Version).HasColumnName("version").HasMaxLength(40).IsRequired();
        builder.Property(prompt => prompt.Template).HasColumnName("template").HasMaxLength(20_000).IsRequired();
        builder.Property(prompt => prompt.IsActive).HasColumnName("is_active");
        builder.Property(prompt => prompt.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(prompt => prompt.CreatedAt).HasColumnName("created_at");
        builder.Property(prompt => prompt.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(prompt => prompt.Version).IsUnique();
        builder.HasIndex(prompt => prompt.IsActive)
            .IsUnique()
            .HasFilter("\"is_active\"")
            .HasDatabaseName("ux_evaluation_prompt_versions_active");
    }
}
