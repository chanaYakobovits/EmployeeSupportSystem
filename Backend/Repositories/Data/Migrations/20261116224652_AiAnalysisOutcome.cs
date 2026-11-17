using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Data.Migrations
{
    /// <inheritdoc />
    public partial class AiAnalysisOutcome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "Disposition",
                table: "TicketAiAnalyses",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "Verdict",
                table: "TicketAiAnalyses",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            // Existing analyses (task 4.3 and the Seeder) did not record their outcome. Best reconstruction:
            // Verdict by the confidence against the default MinConfidence (0.6) – 1 Valid, 4 LowConfidence;
            // Disposition by the ticket's classification now – 1 Applied (Ai), 4 ManuallyClassified (Manual),
            // otherwise 2 LowConfidence / 3 UnknownCategory by the verdict.
            migrationBuilder.Sql("""
                UPDATE a
                SET a.[Verdict] = CASE WHEN a.[Confidence] >= 0.6 THEN 1 ELSE 4 END,
                    a.[Disposition] = CASE
                        WHEN t.[ClassificationSource] = 1 THEN 1
                        WHEN t.[ClassificationSource] = 3 THEN 4
                        WHEN a.[Confidence] < 0.6 THEN 2
                        ELSE 3 END
                FROM [TicketAiAnalyses] a
                INNER JOIN [Tickets] t ON t.[Id] = a.[TicketId];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Disposition",
                table: "TicketAiAnalyses");

            migrationBuilder.DropColumn(
                name: "Verdict",
                table: "TicketAiAnalyses");
        }
    }
}
