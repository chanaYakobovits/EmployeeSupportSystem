using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Data.Migrations
{
    /// <inheritdoc />
    public partial class DepartmentHrUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HrUserId",
                table: "Departments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_HrUserId",
                table: "Departments",
                column: "HrUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Users_HrUserId",
                table: "Departments",
                column: "HrUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Users_HrUserId",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Departments_HrUserId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "HrUserId",
                table: "Departments");
        }
    }
}
