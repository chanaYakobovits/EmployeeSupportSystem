using Common.DTOs.Auth;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Repositories.Data;
using Repositories.Interfaces;

namespace Repositories.Implementations;

public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    // Email and EmployeeCode are unique across the whole system (unique indexes), so at most one match.
    // The default SQL Server collation is case-insensitive.
    public Task<User?> FindForLoginByEmailAsync(string email, CancellationToken cancellationToken) =>
        db.Users
            .Include(u => u.Roles)
            .Include(u => u.Department)
            .SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<User?> FindForLoginByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken) =>
        db.Users
            .Include(u => u.Roles)
            .Include(u => u.Department)
            .SingleOrDefaultAsync(u => u.EmployeeCode == employeeCode, cancellationToken);

    public async Task<CurrentUserDto?> GetCurrentUserAsync(int userId, CancellationToken cancellationToken)
    {
        var row = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => new
            {
                u.Id,
                u.EmployeeCode,
                u.FirstName,
                u.LastName,
                u.Email,
                u.JobTitle,
                DepartmentId = u.Department.Id,
                DepartmentName = u.Department.Name,
                Roles = u.Roles.Select(r => r.Role).ToList(),
                u.MustChangePassword,
            })
            .SingleOrDefaultAsync(cancellationToken);

        // Role names are formatted after the query: enum → string is not translatable to SQL.
        return row is null
            ? null
            : new CurrentUserDto(
                row.Id,
                row.EmployeeCode,
                row.FirstName,
                row.LastName,
                row.Email,
                row.JobTitle,
                new DepartmentSummaryDto(row.DepartmentId, row.DepartmentName),
                row.Roles.Order().Select(r => r.ToString()).ToList(),
                row.MustChangePassword);
    }
}
