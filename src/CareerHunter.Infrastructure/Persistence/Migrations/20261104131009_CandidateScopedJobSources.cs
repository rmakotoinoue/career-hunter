using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerHunter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CandidateScopedJobSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_job_sources_normalized_name",
                table: "job_sources");

            migrationBuilder.AddColumn<Guid>(
                name: "candidate_profile_id",
                table: "job_sources",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_job_sources_candidate_name",
                table: "job_sources",
                columns: new[] { "candidate_profile_id", "normalized_name" },
                unique: true,
                filter: "\"candidate_profile_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_job_sources_shared_name",
                table: "job_sources",
                column: "normalized_name",
                unique: true,
                filter: "\"candidate_profile_id\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_job_sources_candidate_name",
                table: "job_sources");

            migrationBuilder.DropIndex(
                name: "ux_job_sources_shared_name",
                table: "job_sources");

            migrationBuilder.DropColumn(
                name: "candidate_profile_id",
                table: "job_sources");

            migrationBuilder.CreateIndex(
                name: "IX_job_sources_normalized_name",
                table: "job_sources",
                column: "normalized_name",
                unique: true);
        }
    }
}
