using Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Repositories.Interfaces;

namespace Repositories.Data.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name).IsRequired().HasMaxLength(100);

        builder.HasOne(d => d.Organization)
            .WithMany(o => o.Departments)
            .HasForeignKey(d => d.OrganizationId);

        // Circular with Users.DepartmentId: the manager is a user, who also belongs to a department.
        builder.HasOne(d => d.Manager)
            .WithMany()
            .HasForeignKey(d => d.ManagerUserId)
            .IsRequired(false);

        // Department names are unique within an organization (task 2.3).
        builder.HasIndex(d => new { d.OrganizationId, d.Name }).IsUnique().HasDatabaseName(UniqueIndexes.DepartmentName);
    }
}
