using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    ChangedByUserId = table.Column<int>(type: "int", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FromCategoryId = table.Column<int>(type: "int", nullable: true),
                    ToCategoryId = table.Column<int>(type: "int", nullable: true),
                    FromUrgency = table.Column<byte>(type: "tinyint", nullable: true),
                    ToUrgency = table.Column<byte>(type: "tinyint", nullable: true),
                    FromDepartmentId = table.Column<int>(type: "int", nullable: true),
                    ToDepartmentId = table.Column<int>(type: "int", nullable: true),
                    FromUserId = table.Column<int>(type: "int", nullable: true),
                    ToUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketChanges_Categories_FromCategoryId",
                        column: x => x.FromCategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketChanges_Categories_ToCategoryId",
                        column: x => x.ToCategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketChanges_Departments_FromDepartmentId",
                        column: x => x.FromDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketChanges_Departments_ToDepartmentId",
                        column: x => x.ToDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketChanges_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketChanges_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketChanges_Users_FromUserId",
                        column: x => x.FromUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketChanges_Users_ToUserId",
                        column: x => x.ToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketChanges_ChangedByUserId",
                table: "TicketChanges",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketChanges_FromCategoryId",
                table: "TicketChanges",
                column: "FromCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketChanges_FromDepartmentId",
                table: "TicketChanges",
                column: "FromDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketChanges_FromUserId",
                table: "TicketChanges",
                column: "FromUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketChanges_TicketId_ChangedAt",
                table: "TicketChanges",
                columns: new[] { "TicketId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketChanges_ToCategoryId",
                table: "TicketChanges",
                column: "ToCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketChanges_ToDepartmentId",
                table: "TicketChanges",
                column: "ToDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketChanges_ToUserId",
                table: "TicketChanges",
                column: "ToUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketChanges");
        }
    }
}
