using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Data.Migrations
{
    /// <inheritdoc />
    public partial class UserAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Departments_OrganizationId",
                table: "Departments");

            migrationBuilder.CreateSequence(
                name: "EmployeeCodeNumbers");

            // Existing databases already have users: continue after the highest EMP-##### code.
            migrationBuilder.Sql(EmployeeCodeSequence.RestartAfterExistingCodesSql);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_OrganizationId_Name",
                table: "Departments",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Departments_OrganizationId_Name",
                table: "Departments");

            migrationBuilder.DropSequence(
                name: "EmployeeCodeNumbers");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_OrganizationId",
                table: "Departments",
                column: "OrganizationId");
        }
    }
}
