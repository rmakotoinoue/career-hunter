using CareerHunter.Domain.Candidates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerHunter.Infrastructure.Persistence.Configurations;

public sealed class CandidateProfileConfiguration : IEntityTypeConfiguration<CandidateProfile>
{
    public void Configure(EntityTypeBuilder<CandidateProfile> builder)
    {
        builder.ToTable("candidate_profiles");
        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.Id).HasColumnName("id");
        builder.Property(profile => profile.FullName).HasColumnName("full_name").HasMaxLength(120).IsRequired();
        builder.Property(profile => profile.Headline).HasColumnName("headline").HasMaxLength(160).IsRequired();
        builder.Property(profile => profile.Summary).HasColumnName("summary").HasMaxLength(4000);
        builder.Property(profile => profile.LinkedInUrl).HasColumnName("linkedin_url").HasMaxLength(2048);
        builder.Property(profile => profile.YearsOfExperience).HasColumnName("years_of_experience");
        builder.Property(profile => profile.CreatedAt).HasColumnName("created_at");
        builder.Property(profile => profile.UpdatedAt).HasColumnName("updated_at");

        builder.OwnsOne(profile => profile.Email, email =>
        {
            email.Property(value => value.Value).HasColumnName("email").HasMaxLength(254).IsRequired();
        });

        builder.OwnsOne(profile => profile.Location, location =>
        {
            location.Property(value => value.City).HasColumnName("city").HasMaxLength(120).IsRequired();
            location.Property(value => value.State).HasColumnName("state").HasMaxLength(60);
            location.Property(value => value.Country).HasColumnName("country").HasMaxLength(60).IsRequired();
        });

        builder.OwnsOne(profile => profile.CompensationExpectation, expectation =>
        {
            expectation.ToTable("candidate_compensation_expectations");
            OwnedMoneyMapping.MapSalaryRange(expectation, "compensation_expectation");
        });

        builder.OwnsOne(profile => profile.WorkPreferences, preferences =>
        {
            // Declared preferences are optional until the candidate fills them in, so they live
            // in their own table: no row means "not declared" without nullable boolean columns.
            preferences.ToTable("candidate_work_preferences");
            preferences.Property(value => value.AcceptsRemote).HasColumnName("accepts_remote");
            preferences.Property(value => value.AcceptsHybrid).HasColumnName("accepts_hybrid");
            preferences.Property(value => value.AcceptsOnsite).HasColumnName("accepts_onsite");
            preferences.Property(value => value.AcceptsClt).HasColumnName("accepts_clt");
            preferences.Property(value => value.AcceptsPj).HasColumnName("accepts_pj");
        });

        builder.HasMany(profile => profile.Experiences)
            .WithOne()
            .HasForeignKey(experience => experience.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(profile => profile.Skills)
            .WithOne()
            .HasForeignKey(skill => skill.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(profile => profile.Educations)
            .WithOne()
            .HasForeignKey(education => education.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(profile => profile.Certifications)
            .WithOne()
            .HasForeignKey(certification => certification.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(profile => profile.CareerTargets)
            .WithOne()
            .HasForeignKey(target => target.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(profile => profile.Experiences).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(profile => profile.Skills).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(profile => profile.Educations).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(profile => profile.Certifications).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(profile => profile.CareerTargets).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class CandidateExperienceConfiguration : IEntityTypeConfiguration<CandidateExperience>
{
    public void Configure(EntityTypeBuilder<CandidateExperience> builder)
    {
        builder.ToTable("candidate_experiences");
        builder.HasKey(experience => experience.Id);
        builder.Property(experience => experience.Id).HasColumnName("id");
        builder.Property(experience => experience.CandidateProfileId).HasColumnName("candidate_profile_id");
        builder.Property(experience => experience.Employer).HasColumnName("employer").HasMaxLength(160).IsRequired();
        builder.Property(experience => experience.Title).HasColumnName("title").HasMaxLength(160).IsRequired();
        builder.Property(experience => experience.StartDate).HasColumnName("start_date").HasColumnType("date");
        builder.Property(experience => experience.EndDate).HasColumnName("end_date").HasColumnType("date");
        builder.Property(experience => experience.Description).HasColumnName("description").HasMaxLength(4000);
        builder.Ignore(experience => experience.IsCurrent);
        builder.Property(experience => experience.Technologies).HasJsonListConversion("technologies");
        builder.HasIndex(experience => new { experience.CandidateProfileId, experience.StartDate });
    }
}

public sealed class CandidateSkillConfiguration : IEntityTypeConfiguration<CandidateSkill>
{
    public void Configure(EntityTypeBuilder<CandidateSkill> builder)
    {
        builder.ToTable("candidate_skills");
        builder.HasKey(skill => skill.Id);
        builder.Property(skill => skill.Id).HasColumnName("id");
        builder.Property(skill => skill.CandidateProfileId).HasColumnName("candidate_profile_id");
        builder.Property(skill => skill.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
        builder.Property(skill => skill.NormalizedName).HasColumnName("normalized_name").HasMaxLength(80).IsRequired();
        builder.Property(skill => skill.Category).HasColumnName("category").HasConversion<string>().HasMaxLength(30);
        builder.Property(skill => skill.Proficiency).HasColumnName("proficiency");
        builder.Property(skill => skill.YearsOfExperience).HasColumnName("years_of_experience").HasPrecision(4, 1);
        builder.HasIndex(skill => new { skill.CandidateProfileId, skill.NormalizedName }).IsUnique();
    }
}

public sealed class EducationConfiguration : IEntityTypeConfiguration<Education>
{
    public void Configure(EntityTypeBuilder<Education> builder)
    {
        builder.ToTable("candidate_educations");
        builder.HasKey(education => education.Id);
        builder.Property(education => education.Id).HasColumnName("id");
        builder.Property(education => education.CandidateProfileId).HasColumnName("candidate_profile_id");
        builder.Property(education => education.Institution).HasColumnName("institution").HasMaxLength(160).IsRequired();
        builder.Property(education => education.Degree).HasColumnName("degree").HasMaxLength(160).IsRequired();
        builder.Property(education => education.FieldOfStudy).HasColumnName("field_of_study").HasMaxLength(120);
        builder.Property(education => education.StartDate).HasColumnName("start_date").HasColumnType("date");
        builder.Property(education => education.EndDate).HasColumnName("end_date").HasColumnType("date");
        builder.Ignore(education => education.IsInProgress);
    }
}

public sealed class CertificationConfiguration : IEntityTypeConfiguration<Certification>
{
    public void Configure(EntityTypeBuilder<Certification> builder)
    {
        builder.ToTable("candidate_certifications");
        builder.HasKey(certification => certification.Id);
        builder.Property(certification => certification.Id).HasColumnName("id");
        builder.Property(certification => certification.CandidateProfileId).HasColumnName("candidate_profile_id");
        builder.Property(certification => certification.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
        builder.Property(certification => certification.Issuer).HasColumnName("issuer").HasMaxLength(160).IsRequired();
        builder.Property(certification => certification.IssuedOn).HasColumnName("issued_on").HasColumnType("date");
        builder.Property(certification => certification.ExpiresOn).HasColumnName("expires_on").HasColumnType("date");
        builder.Property(certification => certification.CredentialUrl).HasColumnName("credential_url").HasMaxLength(2048);
    }
}

public sealed class CareerTargetConfiguration : IEntityTypeConfiguration<CareerTarget>
{
    public void Configure(EntityTypeBuilder<CareerTarget> builder)
    {
        builder.ToTable("candidate_career_targets");
        builder.HasKey(target => target.Id);
        builder.Property(target => target.Id).HasColumnName("id");
        builder.Property(target => target.CandidateProfileId).HasColumnName("candidate_profile_id");
        builder.Property(target => target.JobTitle).HasColumnName("job_title").HasMaxLength(160).IsRequired();
        builder.Property(target => target.NormalizedJobTitle).HasColumnName("normalized_job_title").HasMaxLength(160).IsRequired();
        builder.Property(target => target.Seniority).HasColumnName("seniority").HasConversion<string>().HasMaxLength(30);
        builder.Property(target => target.IsPrimary).HasColumnName("is_primary");
        builder.Property(target => target.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.HasIndex(target => new { target.CandidateProfileId, target.NormalizedJobTitle }).IsUnique();

        // At most one primary career target per candidate.
        builder.HasIndex(target => target.CandidateProfileId)
            .IsUnique()
            .HasFilter("\"is_primary\"")
            .HasDatabaseName("ux_candidate_career_targets_primary");
    }
}
