using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrmIntegration.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSynchronizationEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "SyncJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "SyncJobs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "SyncJobs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureCategory",
                table: "SyncJobs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InternalEntityId",
                table: "SyncJobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedAt",
                table: "EntityMappings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncJobs_CorrelationId",
                table: "SyncJobs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncJobs_EntityType_InternalEntityId",
                table: "SyncJobs",
                columns: new[] { "EntityType", "InternalEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncJobs_Status",
                table: "SyncJobs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SyncJobs_CorrelationId",
                table: "SyncJobs");

            migrationBuilder.DropIndex(
                name: "IX_SyncJobs_EntityType_InternalEntityId",
                table: "SyncJobs");

            migrationBuilder.DropIndex(
                name: "IX_SyncJobs_Status",
                table: "SyncJobs");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "SyncJobs");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "SyncJobs");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "SyncJobs");

            migrationBuilder.DropColumn(
                name: "FailureCategory",
                table: "SyncJobs");

            migrationBuilder.DropColumn(
                name: "InternalEntityId",
                table: "SyncJobs");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "EntityMappings");
        }
    }
}
