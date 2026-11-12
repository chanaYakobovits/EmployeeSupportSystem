using Common.DTOs.Departments;
using Common.DTOs.Users;
using Common.Enums;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Repositories.Data;
using Repositories.Interfaces;

namespace Repositories.Implementations;

public sealed class DepartmentRepository(AppDbContext db) : IDepartmentRepository
{
    public async Task<IReadOnlyList<DepartmentDto>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken) =>
        // Ordered before the projection: EF cannot translate a member of a constructor-built DTO.
        await Project(db.Departments.AsNoTracking().Where(d => includeInactive || d.IsActive).OrderBy(d => d.Name))
            .ToListAsync(cancellationToken);

    public Task<DepartmentDto?> GetDtoAsync(int departmentId, CancellationToken cancellationToken) =>
        Project(db.Departments.AsNoTracking().Where(d => d.Id == departmentId))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Department?> FindForUpdateAsync(int departmentId, CancellationToken cancellationToken) =>
        db.Departments.SingleOrDefaultAsync(d => d.Id == departmentId, cancellationToken);

    public Task<string?> GetActiveNameAsync(int departmentId, CancellationToken cancellationToken) =>
        db.Departments
            .Where(d => d.Id == departmentId && d.IsActive)
            .Select(d => d.Name)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> NameExistsAsync(string name, int? excludeDepartmentId, CancellationToken cancellationToken) =>
        db.Departments.AnyAsync(d => d.Name == name && (excludeDepartmentId == null || d.Id != excludeDepartmentId),
            cancellationToken);

    public Task<Department?> FindManagedByAsync(int userId, CancellationToken cancellationToken) =>
        db.Departments.FirstOrDefaultAsync(d => d.ManagerUserId == userId, cancellationToken);

    public Task<int> CountActiveUsersAsync(int departmentId, CancellationToken cancellationToken) =>
        db.Users.CountAsync(u => u.DepartmentId == departmentId && u.IsActive, cancellationToken);

    public Task<bool> IsHandlingDepartmentOfActiveCategoryAsync(int departmentId, CancellationToken cancellationToken) =>
        db.Categories.AnyAsync(c => c.HandlingDepartmentId == departmentId && c.IsActive, cancellationToken);

    public void Add(Department department) => db.Departments.Add(department);

    private static IQueryable<DepartmentDto> Project(IQueryable<Department> departments) =>
        departments.Select(d => new DepartmentDto(
            d.Id,
            d.Name,
            d.IsActive,
            d.Manager == null
                ? null
                : new UserSummaryDto(d.Manager.Id, d.Manager.EmployeeCode, d.Manager.FirstName, d.Manager.LastName),
            d.Users.Count(u => u.IsActive),
            d.Users.Count(u => u.IsActive && u.Roles.Any(r => r.Role == Role.Handler))));
}
