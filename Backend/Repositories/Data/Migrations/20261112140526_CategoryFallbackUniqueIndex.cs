using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Data.Migrations
{
    /// <inheritdoc />
    public partial class CategoryFallbackUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Categories_OrganizationId_Fallback",
                table: "Categories",
                column: "OrganizationId",
                unique: true,
                filter: "[IsFallback] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Categories_OrganizationId_Fallback",
                table: "Categories");
        }
    }
}
