using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefreshTokenRotationConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Model-only change: RefreshToken.RevokedAt became a concurrency token (no schema change).
            // Kept so the model snapshot matches; otherwise EF 9 refuses Migrate() with PendingModelChanges.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
