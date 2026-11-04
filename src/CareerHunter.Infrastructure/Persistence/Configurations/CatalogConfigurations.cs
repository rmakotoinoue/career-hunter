using CareerHunter.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerHunter.Infrastructure.Persistence.Configurations;

public sealed class EmployerConfiguration : IEntityTypeConfiguration<Employer>
{
    public void Configure(EntityTypeBuilder<Employer> builder)
    {
        builder.ToTable("employers");
        builder.HasKey(employer => employer.Id);
        builder.Property(employer => employer.Id).HasColumnName("id");
        builder.Property(employer => employer.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(employer => employer.NormalizedName).HasColumnName("normalized_name").HasMaxLength(200).IsRequired();
        builder.Property(employer => employer.Website).HasColumnName("website").HasMaxLength(2048);
        builder.Property(employer => employer.CreatedAt).HasColumnName("created_at");
        builder.Property(employer => employer.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(employer => employer.NormalizedName).IsUnique();
    }
}

public sealed class JobOpportunityConfiguration : IEntityTypeConfiguration<JobOpportunity>
{
    public void Configure(EntityTypeBuilder<JobOpportunity> builder)
    {
        builder.ToTable("job_opportunities");
        builder.HasKey(job => job.Id);
        builder.Property(job => job.Id).HasColumnName("id");
        builder.Property(job => job.EmployerId).HasColumnName("employer_id");
        builder.Property(job => job.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(job => job.NormalizedTitle).HasColumnName("normalized_title").HasMaxLength(200).IsRequired();
        builder.Property(job => job.Modality).HasColumnName("modality").HasConversion<string>().HasMaxLength(30);
        builder.Property(job => job.ContractType).HasColumnName("contract_type").HasConversion<string>().HasMaxLength(30);
        builder.Property(job => job.Seniority).HasColumnName("seniority").HasConversion<string>().HasMaxLength(30);
        builder.Property(job => job.Description).HasColumnName("description").HasMaxLength(40_000).IsRequired();
        builder.Property(job => job.PublishedAt).HasColumnName("published_at");
        builder.Property(job => job.FirstSeenAt).HasColumnName("first_seen_at");
        builder.Property(job => job.LastSeenAt).HasColumnName("last_seen_at");
        builder.Property(job => job.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        builder.Property(job => job.ExpiredAt).HasColumnName("expired_at");
        builder.Property(job => job.DeduplicationKey).HasColumnName("deduplication_key").HasMaxLength(600).IsRequired();
        builder.Property(job => job.CreatedAt).HasColumnName("created_at");
        builder.Property(job => job.UpdatedAt).HasColumnName("updated_at");
        builder.Ignore(job => job.IsActive);

        builder.OwnsOne(job => job.Location, location =>
        {
            location.Property(value => value.City).HasColumnName("city").HasMaxLength(120);
            location.Property(value => value.State).HasColumnName("state").HasMaxLength(60);
            location.Property(value => value.Country).HasColumnName("country").HasMaxLength(60).IsRequired();
        });

        builder.OwnsOne(job => job.Compensation, compensation =>
        {
            // Unknown salary means "no row": zero and null are never conflated.
            compensation.ToTable("job_compensations");
            compensation.Property(value => value.MinAmount).HasColumnName("salary_min").HasPrecision(18, 2);
            compensation.Property(value => value.MaxAmount).HasColumnName("salary_max").HasPrecision(18, 2);
            compensation.Property(value => value.Currency).HasColumnName("salary_currency").HasMaxLength(3).IsRequired();
            compensation.Property(value => value.Period).HasColumnName("salary_period").HasConversion<string>().HasMaxLength(20);
            compensation.Property(value => value.IsEstimated).HasColumnName("salary_is_estimated");
            compensation.Ignore(value => value.IsKnown);
        });

        builder.HasMany(job => job.Requirements)
            .WithOne()
            .HasForeignKey(requirement => requirement.JobOpportunityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(job => job.SourceReferences)
            .WithOne()
            .HasForeignKey(reference => reference.JobOpportunityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(job => job.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(job => job.SourceReferences).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(job => job.DeduplicationKey).IsUnique();
        builder.HasIndex(job => new { job.Status, job.LastSeenAt });
        builder.HasIndex(job => new { job.EmployerId, job.NormalizedTitle });
        builder.HasIndex(job => job.PublishedAt);
    }
}

public sealed class JobRequirementConfiguration : IEntityTypeConfiguration<JobRequirement>
{
    public void Configure(EntityTypeBuilder<JobRequirement> builder)
    {
        builder.ToTable("job_requirements");
        builder.HasKey(requirement => requirement.Id);
        builder.Property(requirement => requirement.Id).HasColumnName("id");
        builder.Property(requirement => requirement.JobOpportunityId).HasColumnName("job_opportunity_id");
        builder.Property(requirement => requirement.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(requirement => requirement.NormalizedName).HasColumnName("normalized_name").HasMaxLength(200).IsRequired();
        builder.Property(requirement => requirement.Category).HasColumnName("category").HasConversion<string>().HasMaxLength(30);
        builder.Property(requirement => requirement.IsRequired).HasColumnName("is_required");
        builder.Property(requirement => requirement.RawText).HasColumnName("raw_text").HasMaxLength(2000);
        builder.HasIndex(requirement => new { requirement.JobOpportunityId, requirement.NormalizedName }).IsUnique();
    }
}

public sealed class JobSourceReferenceConfiguration : IEntityTypeConfiguration<JobSourceReference>
{
    public void Configure(EntityTypeBuilder<JobSourceReference> builder)
    {
        builder.ToTable("job_source_references");
        builder.HasKey(reference => reference.Id);
        builder.Property(reference => reference.Id).HasColumnName("id");
        builder.Property(reference => reference.JobOpportunityId).HasColumnName("job_opportunity_id");
        builder.Property(reference => reference.SourceJobRecordId).HasColumnName("source_job_record_id");
        builder.Property(reference => reference.JobSourceName).HasColumnName("job_source_name").HasMaxLength(120).IsRequired();
        builder.Property(reference => reference.SourceUrl).HasColumnName("source_url").HasMaxLength(2048).IsRequired();
        builder.Property(reference => reference.PublishedAt).HasColumnName("published_at");
        builder.Property(reference => reference.CollectedAt).HasColumnName("collected_at");
        builder.HasIndex(reference => reference.SourceJobRecordId).IsUnique();
        builder.HasIndex(reference => reference.JobOpportunityId);
    }
}
