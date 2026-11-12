using Common.Constants;
using Common.Enums;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Repositories.Data.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    // Statuses in which the SLA clock runs. WaitingForEmployee is excluded because its SLA is paused (spec 3.2).
    private static readonly string OpenStatusFilter =
        $"[Status] IN ({string.Join(", ", TicketStatuses.Open.Select(s => (byte)s))})";

    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Subject).HasMaxLength(150);
        builder.Property(t => t.Description).IsRequired().HasMaxLength(4000);

        // rowversion + concurrency token (spec 11.2).
        builder.Property(t => t.RowVersion).IsRowVersion();

        builder.HasOne(t => t.Organization)
            .WithMany()
            .HasForeignKey(t => t.OrganizationId);

        builder.HasOne(t => t.Requester)
            .WithMany()
            .HasForeignKey(t => t.RequesterId);

        builder.HasOne(t => t.RequesterDepartment)
            .WithMany()
            .HasForeignKey(t => t.RequesterDepartmentId);

        builder.HasOne(t => t.Category)
            .WithMany()
            .HasForeignKey(t => t.CategoryId)
            .IsRequired(false);

        builder.HasOne(t => t.AssignedDepartment)
            .WithMany()
            .HasForeignKey(t => t.AssignedDepartmentId)
            .IsRequired(false);

        builder.HasOne(t => t.AssignedToUser)
            .WithMany()
            .HasForeignKey(t => t.AssignedToUserId)
            .IsRequired(false);

        // Indexes – spec 8.4.
        builder.HasIndex(t => new { t.RequesterId, t.CreatedAt })
            .IsDescending(false, true);

        builder.HasIndex(t => new { t.AssignedToUserId, t.Status })
            .IncludeProperties(t => new { t.DueAt, t.Urgency });

        builder.HasIndex(t => new { t.AssignedDepartmentId, t.Status, t.DueAt });

        builder.HasIndex(t => new { t.RequesterDepartmentId, t.CreatedAt })
            .IncludeProperties(t => new { t.Status, t.CategoryId });

        builder.HasIndex(t => new { t.OrganizationId, t.CreatedAt })
            .IncludeProperties(t => new { t.Status, t.CategoryId, t.RequesterDepartmentId });

        builder.HasIndex(t => t.DueAt)
            .HasDatabaseName("IX_Tickets_DueAt_Open")
            .HasFilter(OpenStatusFilter);
    }
}
