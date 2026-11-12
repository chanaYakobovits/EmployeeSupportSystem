using Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Repositories.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Code).IsRequired().HasMaxLength(30);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Description).IsRequired().HasMaxLength(500);

        builder.HasOne(c => c.Organization)
            .WithMany()
            .HasForeignKey(c => c.OrganizationId);

        builder.HasOne(c => c.HandlingDepartment)
            .WithMany()
            .HasForeignKey(c => c.HandlingDepartmentId)
            .IsRequired(false);

        // Routing and AI resolve categories by code.
        builder.HasIndex(c => new { c.OrganizationId, c.Code }).IsUnique();

        // At most one fallback category (GENERAL) per organization. "At least one" is kept by the services:
        // no endpoint sets or clears IsFallback.
        builder.HasIndex(c => c.OrganizationId)
            .HasDatabaseName("IX_Categories_OrganizationId_Fallback")
            .IsUnique()
            .HasFilter("[IsFallback] = 1");
    }
}
