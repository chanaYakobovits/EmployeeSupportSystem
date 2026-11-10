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

        builder.HasOne(j => j.Ticket)
            .WithMany(t => t.AiAnalysisJobs)
            .HasForeignKey(j => j.TicketId);

       
        builder.HasIndex(j => new { j.Status, j.NextAttemptAt });
    }
}
