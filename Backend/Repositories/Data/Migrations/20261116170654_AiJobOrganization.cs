using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Data.Migrations
{
    /// <inheritdoc />
    public partial class AiJobOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "AiAnalysisJobs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Existing jobs take their ticket's organization (task 4.3: the worker enters it before processing).
            migrationBuilder.Sql("""
                UPDATE j SET j.[OrganizationId] = t.[OrganizationId]
                FROM [AiAnalysisJobs] AS j
                INNER JOIN [Tickets] AS t ON t.[Id] = j.[TicketId];
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AiAnalysisJobs_OrganizationId",
                table: "AiAnalysisJobs",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_AiAnalysisJobs_Organizations_OrganizationId",
                table: "AiAnalysisJobs",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiAnalysisJobs_Organizations_OrganizationId",
                table: "AiAnalysisJobs");

            migrationBuilder.DropIndex(
                name: "IX_AiAnalysisJobs_OrganizationId",
                table: "AiAnalysisJobs");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "AiAnalysisJobs");
        }
    }
}
