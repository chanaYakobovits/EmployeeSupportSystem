using Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Repositories.Interfaces;

namespace Repositories.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.EmployeeCode).IsRequired().HasMaxLength(20);
        builder.Property(u => u.FirstName).IsRequired().HasMaxLength(50);
        builder.Property(u => u.LastName).IsRequired().HasMaxLength(50);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.Property(u => u.JobTitle).IsRequired().HasMaxLength(100);
        // Null while an invited user has not set a password yet.
        builder.Property(u => u.PasswordHash).IsRequired(false).HasMaxLength(256);

        builder.HasOne(u => u.Organization)
            .WithMany(o => o.Users)
            .HasForeignKey(u => u.OrganizationId);

        builder.HasOne(u => u.Department)
            .WithMany(d => d.Users)
            .HasForeignKey(u => u.DepartmentId);

        // Spec 8.4: login by email / employee code. Unique across the whole system (not per organization):
        // login happens before the organization is known, so an identifier must match at most one user.
        // E-mail is stored lower-case (UserService); the default collation is case-insensitive anyway.
        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName(UniqueIndexes.UserEmail);
        builder.HasIndex(u => u.EmployeeCode).IsUnique().HasDatabaseName(UniqueIndexes.UserEmployeeCode);
    }
}
