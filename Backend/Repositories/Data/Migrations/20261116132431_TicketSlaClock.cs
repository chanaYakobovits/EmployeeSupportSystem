using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Data.Migrations
{
    /// <inheritdoc />
    public partial class TicketSlaClock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SlaPausedAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SlaPausedSeconds",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaRestartedAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            // Tickets already waiting for the employee: their pause started when they entered WaitingForEmployee (4).
            migrationBuilder.Sql("""
                UPDATE t SET SlaPausedAt = (
                    SELECT MAX(h.ChangedAt) FROM TicketStatusHistory h
                    WHERE h.TicketId = t.Id AND h.NewStatus = 4)
                FROM Tickets t
                WHERE t.Status = 4;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ResolvedAt_Resolved",
                table: "Tickets",
                column: "ResolvedAt",
                filter: "[Status] = 5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_ResolvedAt_Resolved",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaPausedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaPausedSeconds",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaRestartedAt",
                table: "Tickets");
        }
    }
}
