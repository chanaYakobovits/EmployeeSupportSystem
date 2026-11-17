using Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Repositories.Data.Configurations;

public class AiAnalysisJobConfiguration : IEntityTypeConfiguration<AiAnalysisJob>
{
    public void Configure(EntityTypeBuilder<AiAnalysisJob> builder)
    {
        builder.ToTable("AiAnalysisJobs");
        builder.HasKey(j => j.Id);

        builder.Property(j => j.LastErrorCode).HasMaxLength(50);

        // Every claim increments Attempts. A worker whose lock expired and whose job was claimed again by another server
        // fails on its next save (UPDATE ... WHERE Attempts = <its value>) instead of writing a second result.
        builder.Property(j => j.Attempts).IsConcurrencyToken();

        builder.HasOne(j => j.Ticket)
            .WithMany(t => t.AiAnalysisJobs)
            .HasForeignKey(j => j.TicketId);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(j => j.OrganizationId);

        // The worker's claim query (spec 8.4).
        builder.HasIndex(j => new { j.Status, j.NextAttemptAt });
    }
}
