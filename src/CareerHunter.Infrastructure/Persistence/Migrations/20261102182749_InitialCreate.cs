using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerHunter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidate_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    headline = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    linkedin_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    years_of_experience = table.Column<int>(type: "integer", nullable: true),
                    city = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    state = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    country = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "collection_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    records_collected = table.Column<int>(type: "integer", nullable: false),
                    records_skipped = table.Column<int>(type: "integer", nullable: false),
                    records_rejected = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "daily_digests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    digest_date = table.Column<DateOnly>(type: "date", nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_digests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "employers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    website = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_prompt_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    template = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_prompt_versions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "job_applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_opportunity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_evaluation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    job_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    employer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    job_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_applications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "job_evaluations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_opportunity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    engine = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    overall_score = table.Column<int>(type: "integer", nullable: false),
                    recommendation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    model_provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    model_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    prompt_version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    input_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    strengths = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    gaps = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    assumptions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_evaluations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "job_opportunities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    city = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    state = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    country = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    modality = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    contract_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    seniority = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    description = table.Column<string>(type: "character varying(40000)", maxLength: 40000, nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    expired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deduplication_key = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_opportunities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "job_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    base_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_policy_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    minimum_interval_minutes = table.Column<int>(type: "integer", nullable: false),
                    last_collected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_collection_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_sources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    send_hour_local = table.Column<int>(type: "integer", nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    minimum_score = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_preferences", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "source_job_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    collection_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    source_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    raw_title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    raw_company = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    raw_location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    raw_description = table.Column<string>(type: "character varying(40000)", maxLength: 40000, nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    collected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    normalized_job_opportunity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_job_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "candidate_career_targets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    normalized_job_title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    seniority = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_career_targets", x => x.id);
                    table.ForeignKey(
                        name: "FK_candidate_career_targets_candidate_profiles_candidate_profi~",
                        column: x => x.candidate_profile_id,
                        principalTable: "candidate_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "candidate_certifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    issuer = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    issued_on = table.Column<DateOnly>(type: "date", nullable: false),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    credential_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_certifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_candidate_certifications_candidate_profiles_candidate_profi~",
                        column: x => x.candidate_profile_id,
                        principalTable: "candidate_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "candidate_compensation_expectations",
                columns: table => new
                {
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    compensation_expectation_min = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    compensation_expectation_max = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    compensation_expectation_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    compensation_expectation_period = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_compensation_expectations", x => x.CandidateProfileId);
                    table.ForeignKey(
                        name: "FK_candidate_compensation_expectations_candidate_profiles_Cand~",
                        column: x => x.CandidateProfileId,
                        principalTable: "candidate_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "candidate_educations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    degree = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    field_of_study = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_educations", x => x.id);
                    table.ForeignKey(
                        name: "FK_candidate_educations_candidate_profiles_candidate_profile_id",
                        column: x => x.candidate_profile_id,
                        principalTable: "candidate_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "candidate_experiences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employer = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    technologies = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_experiences", x => x.id);
                    table.ForeignKey(
                        name: "FK_candidate_experiences_candidate_profiles_candidate_profile_~",
                        column: x => x.candidate_profile_id,
                        principalTable: "candidate_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "candidate_skills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    proficiency = table.Column<int>(type: "integer", nullable: false),
                    years_of_experience = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_skills", x => x.id);
                    table.ForeignKey(
                        name: "FK_candidate_skills_candidate_profiles_candidate_profile_id",
                        column: x => x.candidate_profile_id,
                        principalTable: "candidate_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "candidate_work_preferences",
                columns: table => new
                {
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    accepts_remote = table.Column<bool>(type: "boolean", nullable: false),
                    accepts_hybrid = table.Column<bool>(type: "boolean", nullable: false),
                    accepts_onsite = table.Column<bool>(type: "boolean", nullable: false),
                    accepts_clt = table.Column<bool>(type: "boolean", nullable: false),
                    accepts_pj = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_work_preferences", x => x.CandidateProfileId);
                    table.ForeignKey(
                        name: "FK_candidate_work_preferences_candidate_profiles_CandidateProf~",
                        column: x => x.CandidateProfileId,
                        principalTable: "candidate_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "collection_errors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    collection_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_errors", x => x.id);
                    table.ForeignKey(
                        name: "FK_collection_errors_collection_runs_collection_run_id",
                        column: x => x.collection_run_id,
                        principalTable: "collection_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "daily_digest_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    daily_digest_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_evaluation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_opportunity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    employer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    recommendation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    job_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_digest_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_daily_digest_items_daily_digests_daily_digest_id",
                        column: x => x.daily_digest_id,
                        principalTable: "daily_digests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    daily_digest_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    attempted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "FK_notification_deliveries_daily_digests_daily_digest_id",
                        column: x => x.daily_digest_id,
                        principalTable: "daily_digests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "application_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    author = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_notes", x => x.id);
                    table.ForeignKey(
                        name: "FK_application_notes_job_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "job_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "application_status_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    to_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    changed_by = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_status_changes", x => x.id);
                    table.ForeignKey(
                        name: "FK_application_status_changes_job_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "job_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_application_salary_expectations",
                columns: table => new
                {
                    JobApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    salary_expectation_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    salary_expectation_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_application_salary_expectations", x => x.JobApplicationId);
                    table.ForeignKey(
                        name: "FK_job_application_salary_expectations_job_applications_JobApp~",
                        column: x => x.JobApplicationId,
                        principalTable: "job_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prepared_application_materials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    prepared_by = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    model_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prepared_application_materials", x => x.id);
                    table.ForeignKey(
                        name: "FK_prepared_application_materials_job_applications_application~",
                        column: x => x.application_id,
                        principalTable: "job_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_dimension_scores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_evaluation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dimension = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    weight = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    rationale = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_dimension_scores", x => x.id);
                    table.ForeignKey(
                        name: "FK_evaluation_dimension_scores_job_evaluations_job_evaluation_~",
                        column: x => x.job_evaluation_id,
                        principalTable: "job_evaluations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "requirement_matches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_evaluation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requirement_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_requirement_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_requirement_matches", x => x.id);
                    table.ForeignKey(
                        name: "FK_requirement_matches_job_evaluations_job_evaluation_id",
                        column: x => x.job_evaluation_id,
                        principalTable: "job_evaluations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_compensations",
                columns: table => new
                {
                    JobOpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    salary_min = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    salary_max = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    salary_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    salary_period = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    salary_is_estimated = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_compensations", x => x.JobOpportunityId);
                    table.ForeignKey(
                        name: "FK_job_compensations_job_opportunities_JobOpportunityId",
                        column: x => x.JobOpportunityId,
                        principalTable: "job_opportunities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_requirements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_opportunity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    raw_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_requirements", x => x.id);
                    table.ForeignKey(
                        name: "FK_job_requirements_job_opportunities_job_opportunity_id",
                        column: x => x.job_opportunity_id,
                        principalTable: "job_opportunities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_source_references",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_opportunity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_job_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_source_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    source_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    collected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_source_references", x => x.id);
                    table.ForeignKey(
                        name: "FK_job_source_references_job_opportunities_job_opportunity_id",
                        column: x => x.job_opportunity_id,
                        principalTable: "job_opportunities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_evidences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requirement_match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_evidences", x => x.id);
                    table.ForeignKey(
                        name: "FK_evaluation_evidences_requirement_matches_requirement_match_~",
                        column: x => x.requirement_match_id,
                        principalTable: "requirement_matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_application_notes_application_id_created_at",
                table: "application_notes",
                columns: new[] { "application_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_application_status_changes_application_id_changed_at",
                table: "application_status_changes",
                columns: new[] { "application_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_career_targets_candidate_profile_id_normalized_jo~",
                table: "candidate_career_targets",
                columns: new[] { "candidate_profile_id", "normalized_job_title" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_candidate_career_targets_primary",
                table: "candidate_career_targets",
                column: "candidate_profile_id",
                unique: true,
                filter: "\"is_primary\"");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_certifications_candidate_profile_id",
                table: "candidate_certifications",
                column: "candidate_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_educations_candidate_profile_id",
                table: "candidate_educations",
                column: "candidate_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_experiences_candidate_profile_id_start_date",
                table: "candidate_experiences",
                columns: new[] { "candidate_profile_id", "start_date" });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_skills_candidate_profile_id_normalized_name",
                table: "candidate_skills",
                columns: new[] { "candidate_profile_id", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_collection_errors_collection_run_id",
                table: "collection_errors",
                column: "collection_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_collection_runs_job_source_id_started_at",
                table: "collection_runs",
                columns: new[] { "job_source_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "IX_daily_digest_items_daily_digest_id_rank",
                table: "daily_digest_items",
                columns: new[] { "daily_digest_id", "rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_daily_digests_candidate_profile_id_digest_date",
                table: "daily_digests",
                columns: new[] { "candidate_profile_id", "digest_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_daily_digests_status",
                table: "daily_digests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_employers_normalized_name",
                table: "employers",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_dimension_scores_job_evaluation_id_dimension",
                table: "evaluation_dimension_scores",
                columns: new[] { "job_evaluation_id", "dimension" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_evidences_requirement_match_id",
                table: "evaluation_evidences",
                column: "requirement_match_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_prompt_versions_version",
                table: "evaluation_prompt_versions",
                column: "version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_evaluation_prompt_versions_active",
                table: "evaluation_prompt_versions",
                column: "is_active",
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "IX_job_applications_candidate_profile_id_job_opportunity_id",
                table: "job_applications",
                columns: new[] { "candidate_profile_id", "job_opportunity_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_applications_status",
                table: "job_applications",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_job_evaluations_candidate_profile_id_job_opportunity_id_eva~",
                table: "job_evaluations",
                columns: new[] { "candidate_profile_id", "job_opportunity_id", "evaluated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_job_evaluations_candidate_profile_id_overall_score",
                table: "job_evaluations",
                columns: new[] { "candidate_profile_id", "overall_score" });

            migrationBuilder.CreateIndex(
                name: "IX_job_evaluations_job_opportunity_id_evaluated_at",
                table: "job_evaluations",
                columns: new[] { "job_opportunity_id", "evaluated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_job_opportunities_deduplication_key",
                table: "job_opportunities",
                column: "deduplication_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_opportunities_employer_id_normalized_title",
                table: "job_opportunities",
                columns: new[] { "employer_id", "normalized_title" });

            migrationBuilder.CreateIndex(
                name: "IX_job_opportunities_published_at",
                table: "job_opportunities",
                column: "published_at");

            migrationBuilder.CreateIndex(
                name: "IX_job_opportunities_status_last_seen_at",
                table: "job_opportunities",
                columns: new[] { "status", "last_seen_at" });

            migrationBuilder.CreateIndex(
                name: "IX_job_requirements_job_opportunity_id_normalized_name",
                table: "job_requirements",
                columns: new[] { "job_opportunity_id", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_source_references_job_opportunity_id",
                table: "job_source_references",
                column: "job_opportunity_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_source_references_source_job_record_id",
                table: "job_source_references",
                column: "source_job_record_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_sources_normalized_name",
                table: "job_sources",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_deliveries_daily_digest_id_attempted_at",
                table: "notification_deliveries",
                columns: new[] { "daily_digest_id", "attempted_at" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_preferences_candidate_profile_id",
                table: "notification_preferences",
                column: "candidate_profile_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prepared_application_materials_application_id_type_version",
                table: "prepared_application_materials",
                columns: new[] { "application_id", "type", "version" });

            migrationBuilder.CreateIndex(
                name: "IX_requirement_matches_job_evaluation_id_normalized_requiremen~",
                table: "requirement_matches",
                columns: new[] { "job_evaluation_id", "normalized_requirement_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_source_job_records_job_source_id_content_hash",
                table: "source_job_records",
                columns: new[] { "job_source_id", "content_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_source_job_records_normalized_job_opportunity_id",
                table: "source_job_records",
                column: "normalized_job_opportunity_id");

            migrationBuilder.CreateIndex(
                name: "IX_source_job_records_status_collected_at",
                table: "source_job_records",
                columns: new[] { "status", "collected_at" });

            migrationBuilder.CreateIndex(
                name: "ux_source_job_records_source_external_id",
                table: "source_job_records",
                columns: new[] { "job_source_id", "external_id" },
                unique: true,
                filter: "\"external_id\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_notes");

            migrationBuilder.DropTable(
                name: "application_status_changes");

            migrationBuilder.DropTable(
                name: "candidate_career_targets");

            migrationBuilder.DropTable(
                name: "candidate_certifications");

            migrationBuilder.DropTable(
                name: "candidate_compensation_expectations");

            migrationBuilder.DropTable(
                name: "candidate_educations");

            migrationBuilder.DropTable(
                name: "candidate_experiences");

            migrationBuilder.DropTable(
                name: "candidate_skills");

            migrationBuilder.DropTable(
                name: "candidate_work_preferences");

            migrationBuilder.DropTable(
                name: "collection_errors");

            migrationBuilder.DropTable(
                name: "daily_digest_items");

            migrationBuilder.DropTable(
                name: "employers");

            migrationBuilder.DropTable(
                name: "evaluation_dimension_scores");

            migrationBuilder.DropTable(
                name: "evaluation_evidences");

            migrationBuilder.DropTable(
                name: "evaluation_prompt_versions");

            migrationBuilder.DropTable(
                name: "job_application_salary_expectations");

            migrationBuilder.DropTable(
                name: "job_compensations");

            migrationBuilder.DropTable(
                name: "job_requirements");

            migrationBuilder.DropTable(
                name: "job_source_references");

            migrationBuilder.DropTable(
                name: "job_sources");

            migrationBuilder.DropTable(
                name: "notification_deliveries");

            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "prepared_application_materials");

            migrationBuilder.DropTable(
                name: "source_job_records");

            migrationBuilder.DropTable(
                name: "candidate_profiles");

            migrationBuilder.DropTable(
                name: "collection_runs");

            migrationBuilder.DropTable(
                name: "requirement_matches");

            migrationBuilder.DropTable(
                name: "job_opportunities");

            migrationBuilder.DropTable(
                name: "daily_digests");

            migrationBuilder.DropTable(
                name: "job_applications");

            migrationBuilder.DropTable(
                name: "job_evaluations");
        }
    }
}
