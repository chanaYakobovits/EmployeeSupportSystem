using Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Repositories.Data.Configurations;

public class UserInvitationConfiguration : IEntityTypeConfiguration<UserInvitation>
{
    public void Configure(EntityTypeBuilder<UserInvitation> builder)
    {
        builder.ToTable("UserInvitations");
        builder.HasKey(i => i.Id);

        // SHA-256 as upper-case hex (SecureTokenGenerator.Hash).
        builder.Property(i => i.TokenHash).IsRequired().HasMaxLength(64).IsFixedLength().IsUnicode(false);

        builder.HasOne(i => i.User)
            .WithMany()
            .HasForeignKey(i => i.UserId);

        // Lookup by hash on set-password.
        builder.HasIndex(i => i.TokenHash).IsUnique();
        // Revoking the open invitations of a user.
        builder.HasIndex(i => i.UserId);
    }
}
