using CareerHunter.Domain.Notifications;
using CareerHunter.Domain.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerHunter.Infrastructure.Persistence.Configurations;

public sealed class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> builder)
    {
        builder.ToTable("job_applications");
        builder.HasKey(application => application.Id);
        builder.Property(application => application.Id).HasColumnName("id");
        builder.Property(application => application.CandidateProfileId).HasColumnName("candidate_profile_id");
        builder.Property(application => application.JobOpportunityId).HasColumnName("job_opportunity_id");
        builder.Property(application => application.JobEvaluationId).HasColumnName("job_evaluation_id");
        builder.Property(application => application.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        builder.Property(application => application.JobTitleSnapshot).HasColumnName("job_title").HasMaxLength(200).IsRequired();
        builder.Property(application => application.EmployerNameSnapshot).HasColumnName("employer_name").HasMaxLength(200).IsRequired();
        builder.Property(application => application.JobUrlSnapshot).HasColumnName("job_url").HasMaxLength(2048).IsRequired();
        builder.Property(application => application.AppliedAt).HasColumnName("applied_at");
        builder.Property(application => application.CreatedAt).HasColumnName("created_at");
        builder.Property(application => application.UpdatedAt).HasColumnName("updated_at");
        builder.Ignore(application => application.IsTerminal);

        builder.OwnsOne(application => application.SalaryExpectation, salary =>
        {
            salary.ToTable("job_application_salary_expectations");
            OwnedMoneyMapping.MapMoney(salary, "salary_expectation");
        });

        builder.HasMany(application => application.StatusHistory)
            .WithOne()
            .HasForeignKey(change => change.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(application => application.Materials)
            .WithOne()
            .HasForeignKey(material => material.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(application => application.Notes)
            .WithOne()
            .HasForeignKey(note => note.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(application => application.StatusHistory).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(application => application.Materials).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(application => application.Notes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(application => new { application.CandidateProfileId, application.JobOpportunityId }).IsUnique();
        builder.HasIndex(application => application.Status);
    }
}

public sealed class ApplicationStatusChangeConfiguration : IEntityTypeConfiguration<ApplicationStatusChange>
{
    public void Configure(EntityTypeBuilder<ApplicationStatusChange> builder)
    {
        builder.ToTable("application_status_changes");
        builder.HasKey(change => change.Id);
        builder.Property(change => change.Id).HasColumnName("id");
        builder.Property(change => change.ApplicationId).HasColumnName("application_id");
        builder.Property(change => change.FromStatus).HasColumnName("from_status").HasConversion<string>().HasMaxLength(30);
        builder.Property(change => change.ToStatus).HasColumnName("to_status").HasConversion<string>().HasMaxLength(30);
        builder.Property(change => change.ChangedBy).HasColumnName("changed_by").HasMaxLength(120).IsRequired();
        builder.Property(change => change.Reason).HasColumnName("reason").HasMaxLength(1000);
        builder.Property(change => change.ChangedAt).HasColumnName("changed_at");
        builder.HasIndex(change => new { change.ApplicationId, change.ChangedAt });
    }
}

public sealed class PreparedApplicationMaterialConfiguration : IEntityTypeConfiguration<PreparedApplicationMaterial>
{
    public void Configure(EntityTypeBuilder<PreparedApplicationMaterial> builder)
    {
        builder.ToTable("prepared_application_materials");
        builder.HasKey(material => material.Id);
        builder.Property(material => material.Id).HasColumnName("id");
        builder.Property(material => material.ApplicationId).HasColumnName("application_id");
        builder.Property(material => material.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(30);
        builder.Property(material => material.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(material => material.Content).HasColumnName("content").HasMaxLength(20_000).IsRequired();
        builder.Property(material => material.PreparedBy).HasColumnName("prepared_by").HasMaxLength(80).IsRequired();
        builder.Property(material => material.ModelName).HasColumnName("model_name").HasMaxLength(120);
        builder.Property(material => material.Version).HasColumnName("version");
        builder.Property(material => material.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(material => new { material.ApplicationId, material.Type, material.Version });
    }
}

public sealed class ApplicationNoteConfiguration : IEntityTypeConfiguration<ApplicationNote>
{
    public void Configure(EntityTypeBuilder<ApplicationNote> builder)
    {
        builder.ToTable("application_notes");
        builder.HasKey(note => note.Id);
        builder.Property(note => note.Id).HasColumnName("id");
        builder.Property(note => note.ApplicationId).HasColumnName("application_id");
        builder.Property(note => note.Text).HasColumnName("text").HasMaxLength(4000).IsRequired();
        builder.Property(note => note.Author).HasColumnName("author").HasMaxLength(120).IsRequired();
        builder.Property(note => note.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(note => new { note.ApplicationId, note.CreatedAt });
    }
}

public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preferences");
        builder.HasKey(preference => preference.Id);
        builder.Property(preference => preference.Id).HasColumnName("id");
        builder.Property(preference => preference.CandidateProfileId).HasColumnName("candidate_profile_id");
        builder.Property(preference => preference.IsEnabled).HasColumnName("is_enabled");
        builder.Property(preference => preference.SendHourLocal).HasColumnName("send_hour_local");
        builder.Property(preference => preference.TimeZoneId).HasColumnName("time_zone_id").HasMaxLength(100).IsRequired();
        builder.Property(preference => preference.MinimumScore).HasColumnName("minimum_score");
        builder.Property(preference => preference.CreatedAt).HasColumnName("created_at");
        builder.Property(preference => preference.UpdatedAt).HasColumnName("updated_at");

        builder.OwnsOne(preference => preference.Email, email =>
        {
            email.Property(value => value.Value).HasColumnName("email").HasMaxLength(254).IsRequired();
        });

        builder.HasIndex(preference => preference.CandidateProfileId).IsUnique();
    }
}

public sealed class DailyDigestConfiguration : IEntityTypeConfiguration<DailyDigest>
{
    public void Configure(EntityTypeBuilder<DailyDigest> builder)
    {
        builder.ToTable("daily_digests");
        builder.HasKey(digest => digest.Id);
        builder.Property(digest => digest.Id).HasColumnName("id");
        builder.Property(digest => digest.CandidateProfileId).HasColumnName("candidate_profile_id");
        builder.Property(digest => digest.DigestDate).HasColumnName("digest_date").HasColumnType("date");
        builder.Property(digest => digest.GeneratedAt).HasColumnName("generated_at");
        builder.Property(digest => digest.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(digest => digest.SentAt).HasColumnName("sent_at");
        builder.Property(digest => digest.LastError).HasColumnName("last_error").HasMaxLength(2000);
        builder.Property(digest => digest.CreatedAt).HasColumnName("created_at");
        builder.Property(digest => digest.UpdatedAt).HasColumnName("updated_at");
        builder.Ignore(digest => digest.AttemptCount);
        builder.Ignore(digest => digest.CanBeSent);

        builder.HasMany(digest => digest.Items)
            .WithOne()
            .HasForeignKey(item => item.DailyDigestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(digest => digest.Deliveries)
            .WithOne()
            .HasForeignKey(delivery => delivery.DailyDigestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(digest => digest.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(digest => digest.Deliveries).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(digest => new { digest.CandidateProfileId, digest.DigestDate }).IsUnique();
        builder.HasIndex(digest => digest.Status);
    }
}

public sealed class DailyDigestItemConfiguration : IEntityTypeConfiguration<DailyDigestItem>
{
    public void Configure(EntityTypeBuilder<DailyDigestItem> builder)
    {
        builder.ToTable("daily_digest_items");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.DailyDigestId).HasColumnName("daily_digest_id");
        builder.Property(item => item.JobEvaluationId).HasColumnName("job_evaluation_id");
        builder.Property(item => item.JobOpportunityId).HasColumnName("job_opportunity_id");
        builder.Property(item => item.JobTitle).HasColumnName("job_title").HasMaxLength(200).IsRequired();
        builder.Property(item => item.EmployerName).HasColumnName("employer_name").HasMaxLength(200).IsRequired();
        builder.Property(item => item.Score).HasColumnName("score");
        builder.Property(item => item.Recommendation).HasColumnName("recommendation").HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.JobUrl).HasColumnName("job_url").HasMaxLength(2048).IsRequired();
        builder.Property(item => item.Rank).HasColumnName("rank");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(item => new { item.DailyDigestId, item.Rank }).IsUnique();
    }
}

public sealed class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("notification_deliveries");
        builder.HasKey(delivery => delivery.Id);
        builder.Property(delivery => delivery.Id).HasColumnName("id");
        builder.Property(delivery => delivery.DailyDigestId).HasColumnName("daily_digest_id");
        builder.Property(delivery => delivery.Provider).HasColumnName("provider").HasMaxLength(80).IsRequired();
        builder.Property(delivery => delivery.Succeeded).HasColumnName("succeeded");
        builder.Property(delivery => delivery.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        builder.Property(delivery => delivery.AttemptedAt).HasColumnName("attempted_at");
        builder.HasIndex(delivery => new { delivery.DailyDigestId, delivery.AttemptedAt });
    }
}
