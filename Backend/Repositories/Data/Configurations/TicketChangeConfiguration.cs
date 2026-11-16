using Common.Constants;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Repositories.Data.Configurations;

public class TicketChangeConfiguration : IEntityTypeConfiguration<TicketChange>
{
    public void Configure(EntityTypeBuilder<TicketChange> builder)
    {
        builder.ToTable("TicketChanges");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Reason).HasMaxLength(TicketFieldLimits.ReasonMaxLength);

        builder.HasOne(c => c.Ticket)
            .WithMany(t => t.Changes)
            .HasForeignKey(c => c.TicketId);

        builder.HasOne(c => c.ChangedByUser).WithMany().HasForeignKey(c => c.ChangedByUserId);
        builder.HasOne(c => c.FromCategory).WithMany().HasForeignKey(c => c.FromCategoryId).IsRequired(false);
        builder.HasOne(c => c.ToCategory).WithMany().HasForeignKey(c => c.ToCategoryId).IsRequired(false);
        builder.HasOne(c => c.FromDepartment).WithMany().HasForeignKey(c => c.FromDepartmentId).IsRequired(false);
        builder.HasOne(c => c.ToDepartment).WithMany().HasForeignKey(c => c.ToDepartmentId).IsRequired(false);
        builder.HasOne(c => c.FromUser).WithMany().HasForeignKey(c => c.FromUserId).IsRequired(false);
        builder.HasOne(c => c.ToUser).WithMany().HasForeignKey(c => c.ToUserId).IsRequired(false);

        // The staff timeline of a ticket.
        builder.HasIndex(c => new { c.TicketId, c.ChangedAt });
    }
}
