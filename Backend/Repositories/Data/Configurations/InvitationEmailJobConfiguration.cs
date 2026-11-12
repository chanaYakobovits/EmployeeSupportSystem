using Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Repositories.Data.Configurations;

public class InvitationEmailJobConfiguration : IEntityTypeConfiguration<InvitationEmailJob>
{
    public void Configure(EntityTypeBuilder<InvitationEmailJob> builder)
    {
        builder.ToTable("InvitationEmailJobs");
        builder.HasKey(j => j.Id);

        builder.Property(j => j.LastErrorCode).HasMaxLength(50);

        // A resend / reset cancels open jobs with a bulk UPDATE. If the worker is processing one of them, its next save
        // (UPDATE ... WHERE Status = 'Processing') finds no row and fails with a conflict, so it does not go on as if
        // nothing happened.
        builder.Property(j => j.Status).IsConcurrencyToken();

        builder.HasOne(j => j.User)
            .WithMany()
            .HasForeignKey(j => j.UserId);

        builder.HasOne(j => j.Invitation)
            .WithMany()
            .HasForeignKey(j => j.InvitationId)
            .IsRequired(false);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(j => j.OrganizationId);

        // The worker's claim query.
        builder.HasIndex(j => new { j.Status, j.NextAttemptAt });
        // Latest job of a user (invitation status in the users list).
        builder.HasIndex(j => new { j.UserId, j.CreatedAt });
    }
}
