using Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Repositories.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.TokenHash).IsRequired().HasMaxLength(128);

        // Rotation: UPDATE ... WHERE RevokedAt IS NULL. Of two concurrent refreshes with the same token
        // only one wins; the other gets a concurrency conflict instead of a second valid token.
        builder.Property(rt => rt.RevokedAt).IsConcurrencyToken();

        builder.HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId);

        builder.HasOne(rt => rt.ReplacedByToken)
            .WithMany()
            .HasForeignKey(rt => rt.ReplacedByTokenId)
            .IsRequired(false);

        // Lookup by hash on token refresh.
        builder.HasIndex(rt => rt.TokenHash).IsUnique();
    }
}
