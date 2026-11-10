using Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Repositories.Data.Configurations;

public class TicketAiAnalysisConfiguration : IEntityTypeConfiguration<TicketAiAnalysis>
{
    public void Configure(EntityTypeBuilder<TicketAiAnalysis> builder)
    {
        builder.ToTable("TicketAiAnalyses");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Model).IsRequired().HasMaxLength(50);
        builder.Property(a => a.PromptVersion).IsRequired().HasMaxLength(20);
        builder.Property(a => a.CategoryCode).IsRequired().HasMaxLength(30);
        builder.Property(a => a.Confidence).HasPrecision(4, 3);
        builder.Property(a => a.SuggestedSubject).IsRequired().HasMaxLength(80);
        builder.Property(a => a.Summary).IsRequired().HasMaxLength(300);
        builder.Property(a => a.SuggestedSolution).IsRequired().HasMaxLength(4000);
        builder.Property(a => a.MissingInfoJson).IsRequired().HasMaxLength(4000);
        builder.Property(a => a.RedactedInput).IsRequired(); // nvarchar(max): subject + description after redaction

        // One analysis per ticket (TicketId unique, spec 8.3).
        builder.HasOne(a => a.Ticket)
            .WithOne(t => t.AiAnalysis)
            .HasForeignKey<TicketAiAnalysis>(a => a.TicketId);

        builder.HasIndex(a => a.TicketId).IsUnique();
    }
}
