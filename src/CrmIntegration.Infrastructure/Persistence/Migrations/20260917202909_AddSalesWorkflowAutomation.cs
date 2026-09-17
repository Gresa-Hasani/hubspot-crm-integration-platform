using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrmIntegration.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesWorkflowAutomation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContactId",
                table: "OnboardingRecords",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AutomationExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AutomationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FailureCategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ResultSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationExecutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContactLifecycleTransitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ToStage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IntegrationEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactLifecycleTransitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContactLifecycleTransitions_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DealStageTransitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DealId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ToStage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IntegrationEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealStageTransitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DealStageTransitions_Deals_DealId",
                        column: x => x.DealId,
                        principalTable: "Deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingRecords_ContactId",
                table: "OnboardingRecords",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationExecutions_EntityType_EntityId",
                table: "AutomationExecutions",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationExecutions_IdempotencyKey",
                table: "AutomationExecutions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutomationExecutions_Status",
                table: "AutomationExecutions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ContactLifecycleTransitions_ContactId",
                table: "ContactLifecycleTransitions",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactLifecycleTransitions_OccurredAt",
                table: "ContactLifecycleTransitions",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_DealStageTransitions_DealId",
                table: "DealStageTransitions",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_DealStageTransitions_OccurredAt",
                table: "DealStageTransitions",
                column: "OccurredAt");

            migrationBuilder.AddForeignKey(
                name: "FK_OnboardingRecords_Contacts_ContactId",
                table: "OnboardingRecords",
                column: "ContactId",
                principalTable: "Contacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OnboardingRecords_Contacts_ContactId",
                table: "OnboardingRecords");

            migrationBuilder.DropTable(
                name: "AutomationExecutions");

            migrationBuilder.DropTable(
                name: "ContactLifecycleTransitions");

            migrationBuilder.DropTable(
                name: "DealStageTransitions");

            migrationBuilder.DropIndex(
                name: "IX_OnboardingRecords_ContactId",
                table: "OnboardingRecords");

            migrationBuilder.DropColumn(
                name: "ContactId",
                table: "OnboardingRecords");
        }
    }
}
