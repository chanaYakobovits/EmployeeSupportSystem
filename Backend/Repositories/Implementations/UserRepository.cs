using System.Data;
using Common.DTOs.Auth;
using Common.DTOs.Paging;
using Common.DTOs.Users;
using Common.Enums;
using Common.Exceptions;
using Common.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Repositories.Data;
using Repositories.Interfaces;

namespace Repositories.Implementations;

public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    // Email and EmployeeCode are unique across the whole system (unique indexes), so at most one match.
    // The default SQL Server collation is case-insensitive.
    // IgnoreQueryFilters: login happens before anyone is signed in, so the organization filter would hide every user.
    public Task<User?> FindForLoginByEmailAsync(string email, CancellationToken cancellationToken) =>
        db.Users
            .IgnoreQueryFilters()
            .Include(u => u.Roles)
            .Include(u => u.Department)
            .SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<User?> FindForLoginByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken) =>
        db.Users
            .IgnoreQueryFilters()
            .Include(u => u.Roles)
            .Include(u => u.Department)
            .SingleOrDefaultAsync(u => u.EmployeeCode == employeeCode, cancellationToken);

    // Signed in: the organization filter applies.
    public Task<User?> FindForUpdateAsync(int userId, CancellationToken cancellationToken) =>
        db.Users
            .Include(u => u.Roles)
            .Include(u => u.Department)
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

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

    // ===================== User administration (task 2.3) =====================

    public Task<PagedResult<UserListItemDto>> GetPageAsync(UserListQuery query, CancellationToken cancellationToken)
    {
        var users = db.Users.AsNoTracking();

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            // Contains → LIKE '%…%', with LIKE wildcards in the search text escaped by EF Core.
            users = users.Where(u =>
                u.FirstName.Contains(search) ||
                u.LastName.Contains(search) ||
                (u.FirstName + " " + u.LastName).Contains(search) ||
                (u.LastName + " " + u.FirstName).Contains(search) ||
                u.Email.Contains(search) ||
                u.EmployeeCode.Contains(search));
        }

        if (query.DepartmentId is { } departmentId)
        {
            users = users.Where(u => u.DepartmentId == departmentId);
        }

        if (query.Role is { } role)
        {
            users = users.Where(u => u.Roles.Any(r => r.Role == role));
        }

        if (query.IsActive is { } isActive)
        {
            users = users.Where(u => u.IsActive == isActive);
        }

        var desc = query.IsDescending;
        var ordered = (query.SortBy ?? UserSortFields.Name) switch
        {
            UserSortFields.EmployeeCode => users.OrderByDirection(u => u.EmployeeCode, desc),
            UserSortFields.Email => users.OrderByDirection(u => u.Email, desc),
            UserSortFields.Department => users.OrderByDirection(u => u.Department.Name, desc)
                .ThenBy(u => u.LastName).ThenBy(u => u.FirstName),
            UserSortFields.CreatedAt => users.OrderByDirection(u => u.CreatedAt, desc),
            UserSortFields.LastLoginAt => users.OrderByDirection(u => u.LastLoginAt, desc),
            _ => users.OrderByDirection(u => u.LastName, desc).ThenByDirection(u => u.FirstName, desc),
        };
        // Always ends with Id: a total order, so a row never appears on two pages.
        ordered = ordered.ThenBy(u => u.Id);

        return users.ToPagedResultAsync(ordered, u => new UserListItemDto(
            u.Id,
            u.EmployeeCode,
            u.FirstName,
            u.LastName,
            u.Email,
            u.JobTitle,
            new DepartmentSummaryDto(u.Department.Id, u.Department.Name),
            u.Roles.OrderBy(r => r.Role).Select(r => r.Role).ToList(),
            u.IsActive,
            u.CreatedAt,
            u.LastLoginAt), query, cancellationToken);
    }

    public Task<UserDetailsDto?> GetDetailsAsync(int userId, DateTime now, CancellationToken cancellationToken) =>
        db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserDetailsDto(
                u.Id,
                u.EmployeeCode,
                u.FirstName,
                u.LastName,
                u.Email,
                u.JobTitle,
                new DepartmentSummaryDto(u.Department.Id, u.Department.Name),
                u.Roles.OrderBy(r => r.Role).Select(r => r.Role).ToList(),
                u.IsActive,
                u.MustChangePassword,
                u.LockoutEnd != null && u.LockoutEnd > now,
                u.CreatedAt,
                u.LastLoginAt,
                db.Departments
                    .Where(d => d.ManagerUserId == u.Id)
                    .Select(d => new DepartmentSummaryDto(d.Id, d.Name))
                    .FirstOrDefault()))
            .SingleOrDefaultAsync(cancellationToken);

    // IgnoreQueryFilters: e-mail is unique across all organizations (it is the login identifier).
    // Only a bool leaves this method, so nothing about the other organization is exposed.
    public Task<bool> EmailExistsAsync(string email, int? excludeUserId, CancellationToken cancellationToken) =>
        db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == email && (excludeUserId == null || u.Id != excludeUserId), cancellationToken);

    public async Task<long> NextEmployeeNumberAsync(CancellationToken cancellationToken)
    {
        // Not composed with LINQ, so EF sends the statement as is.
        var values = await db.Database
            .SqlQueryRaw<long>($"SELECT NEXT VALUE FOR [{EmployeeCodeSequence.Name}] AS [Value]")
            .ToListAsync(cancellationToken);
        return values.Single();
    }

    public Task<int> CountActiveAdminsAsync(int? excludeUserId, CancellationToken cancellationToken) =>
        db.Users.CountAsync(u =>
            u.IsActive &&
            u.Roles.Any(r => r.Role == Role.Admin) &&
            (excludeUserId == null || u.Id != excludeUserId), cancellationToken);

    public async Task LockOrganizationAdminsAsync(int organizationId, CancellationToken cancellationToken)
    {
        var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var resource = new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = $"org:{organizationId}:admins" };

        // Released automatically when the transaction commits or rolls back.
        await db.Database.ExecuteSqlRawAsync(
            "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', " +
            "@LockOwner = 'Transaction', @LockTimeout = 10000",
            [result, resource],
            cancellationToken);

        // 0 / 1 = granted; negative = timeout, deadlock or error.
        if (result.Value is not int code || code < 0)
        {
            throw new ConflictException("פעולה אחרת על משתמשים מתבצעת כרגע", "יש לנסות שוב בעוד רגע.", "busy");
        }
    }

    public void Add(User user) => db.Users.Add(user);
}
