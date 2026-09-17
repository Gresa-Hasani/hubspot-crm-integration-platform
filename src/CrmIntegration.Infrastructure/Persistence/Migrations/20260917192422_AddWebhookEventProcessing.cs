using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrmIntegration.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookEventProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RetryCount",
                table: "IntegrationEvents",
                newName: "HubSpotAttemptNumber");

            migrationBuilder.AlterColumn<string>(
                name: "CorrelationId",
                table: "IntegrationEvents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "IntegrationEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FailureCategory",
                table: "IntegrationEvents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OccurredAt",
                table: "IntegrationEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEvents_EntityType_EntityId",
                table: "IntegrationEvents",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEvents_ReceivedAt",
                table: "IntegrationEvents",
                column: "ReceivedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IntegrationEvents_EntityType_EntityId",
                table: "IntegrationEvents");

            migrationBuilder.DropIndex(
                name: "IX_IntegrationEvents_ReceivedAt",
                table: "IntegrationEvents");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "IntegrationEvents");

            migrationBuilder.DropColumn(
                name: "FailureCategory",
                table: "IntegrationEvents");

            migrationBuilder.DropColumn(
                name: "OccurredAt",
                table: "IntegrationEvents");

            migrationBuilder.RenameColumn(
                name: "HubSpotAttemptNumber",
                table: "IntegrationEvents",
                newName: "RetryCount");

            migrationBuilder.AlterColumn<string>(
                name: "CorrelationId",
                table: "IntegrationEvents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);
        }
    }
}
