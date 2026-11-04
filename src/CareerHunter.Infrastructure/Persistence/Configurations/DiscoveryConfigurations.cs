using CareerHunter.Domain.Discovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerHunter.Infrastructure.Persistence.Configurations;

public sealed class JobSourceConfiguration : IEntityTypeConfiguration<JobSource>
{
    public void Configure(EntityTypeBuilder<JobSource> builder)
    {
        builder.ToTable("job_sources");
        builder.HasKey(source => source.Id);
        builder.Property(source => source.Id).HasColumnName("id");
        builder.Property(source => source.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(source => source.NormalizedName).HasColumnName("normalized_name").HasMaxLength(120).IsRequired();
        builder.Property(source => source.BaseUrl).HasColumnName("base_url").HasMaxLength(2048).IsRequired();
        builder.Property(source => source.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(30);
        builder.Property(source => source.CandidateProfileId).HasColumnName("candidate_profile_id");
        builder.Property(source => source.IsEnabled).HasColumnName("is_enabled");
        builder.Property(source => source.AccessPolicyNotes).HasColumnName("access_policy_notes").HasMaxLength(2000);
        builder.Property(source => source.MinimumIntervalMinutes).HasColumnName("minimum_interval_minutes");
        builder.Property(source => source.LastCollectedAt).HasColumnName("last_collected_at");
        builder.Property(source => source.LastCollectionStatus).HasColumnName("last_collection_status").HasConversion<string>().HasMaxLength(30);
        builder.Property(source => source.CreatedAt).HasColumnName("created_at");
        builder.Property(source => source.UpdatedAt).HasColumnName("updated_at");

        // A candidate can register the same board as another candidate; the name stays unique
        // per owner. Shared sources (candidate_profile_id IS NULL) keep a global unique name.
        builder.HasIndex(source => new { source.CandidateProfileId, source.NormalizedName })
            .IsUnique()
            .HasFilter("\"candidate_profile_id\" IS NOT NULL")
            .HasDatabaseName("ux_job_sources_candidate_name");
        builder.HasIndex(source => source.NormalizedName)
            .IsUnique()
            .HasFilter("\"candidate_profile_id\" IS NULL")
            .HasDatabaseName("ux_job_sources_shared_name");
    }
}

public sealed class SourceJobRecordConfiguration : IEntityTypeConfiguration<SourceJobRecord>
{
    public void Configure(EntityTypeBuilder<SourceJobRecord> builder)
    {
        builder.ToTable("source_job_records");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).HasColumnName("id");
        builder.Property(record => record.JobSourceId).HasColumnName("job_source_id");
        builder.Property(record => record.CollectionRunId).HasColumnName("collection_run_id");
        builder.Property(record => record.ExternalId).HasColumnName("external_id").HasMaxLength(200);
        builder.Property(record => record.SourceUrl).HasColumnName("source_url").HasMaxLength(2048).IsRequired();
        builder.Property(record => record.RawTitle).HasColumnName("raw_title").HasMaxLength(300).IsRequired();
        builder.Property(record => record.RawCompany).HasColumnName("raw_company").HasMaxLength(200);
        builder.Property(record => record.RawLocation).HasColumnName("raw_location").HasMaxLength(200);
        builder.Property(record => record.RawDescription).HasColumnName("raw_description").HasMaxLength(40_000).IsRequired();
        builder.Property(record => record.PublishedAt).HasColumnName("published_at");
        builder.Property(record => record.CollectedAt).HasColumnName("collected_at");
        builder.Property(record => record.ContentHash).HasColumnName("content_hash").HasMaxLength(64).IsRequired();
        builder.Property(record => record.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        builder.Property(record => record.NormalizedJobOpportunityId).HasColumnName("normalized_job_opportunity_id");
        builder.Property(record => record.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(500);
        builder.Property(record => record.CreatedAt).HasColumnName("created_at");
        builder.Property(record => record.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(record => new { record.JobSourceId, record.ContentHash }).IsUnique();
        builder.HasIndex(record => new { record.Status, record.CollectedAt });
        builder.HasIndex(record => record.NormalizedJobOpportunityId);
        builder.HasIndex(record => new { record.JobSourceId, record.ExternalId })
            .IsUnique()
            .HasFilter("\"external_id\" IS NOT NULL")
            .HasDatabaseName("ux_source_job_records_source_external_id");
    }
}

public sealed class CollectionRunConfiguration : IEntityTypeConfiguration<CollectionRun>
{
    public void Configure(EntityTypeBuilder<CollectionRun> builder)
    {
        builder.ToTable("collection_runs");
        builder.HasKey(run => run.Id);
        builder.Property(run => run.Id).HasColumnName("id");
        builder.Property(run => run.JobSourceId).HasColumnName("job_source_id");
        builder.Property(run => run.StartedAt).HasColumnName("started_at");
        builder.Property(run => run.CompletedAt).HasColumnName("completed_at");
        builder.Property(run => run.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        builder.Property(run => run.RecordsCollected).HasColumnName("records_collected");
        builder.Property(run => run.RecordsSkipped).HasColumnName("records_skipped");
        builder.Property(run => run.RecordsRejected).HasColumnName("records_rejected");
        builder.Property(run => run.CreatedAt).HasColumnName("created_at");
        builder.Property(run => run.UpdatedAt).HasColumnName("updated_at");

        builder.HasMany(run => run.Errors)
            .WithOne()
            .HasForeignKey(error => error.CollectionRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(run => run.Errors).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(run => new { run.JobSourceId, run.StartedAt });
    }
}

public sealed class CollectionErrorConfiguration : IEntityTypeConfiguration<CollectionError>
{
    public void Configure(EntityTypeBuilder<CollectionError> builder)
    {
        builder.ToTable("collection_errors");
        builder.HasKey(error => error.Id);
        builder.Property(error => error.Id).HasColumnName("id");
        builder.Property(error => error.CollectionRunId).HasColumnName("collection_run_id");
        builder.Property(error => error.Stage).HasColumnName("stage").HasMaxLength(60).IsRequired();
        builder.Property(error => error.Message).HasColumnName("message").HasMaxLength(2000).IsRequired();
        builder.Property(error => error.OccurredAt).HasColumnName("occurred_at");
    }
}
