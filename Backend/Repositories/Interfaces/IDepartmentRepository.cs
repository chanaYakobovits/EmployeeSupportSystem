using Common.DTOs.Departments;
using Common.Models;

namespace Repositories.Interfaces;

/// <summary>Departments of the signed-in user's organization (organization query filter).</summary>
public interface IDepartmentRepository
{
    /// <summary>Ordered by name.</summary>
    Task<IReadOnlyList<DepartmentDto>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<DepartmentDto?> GetDtoAsync(int departmentId, CancellationToken cancellationToken);

    /// <summary>Tracked. Null if not found.</summary>
    Task<Department?> FindForUpdateAsync(int departmentId, CancellationToken cancellationToken);

    /// <summary>Name of the department if it exists and is active; otherwise null.</summary>
    Task<string?> GetActiveNameAsync(int departmentId, CancellationToken cancellationToken);

    /// <summary>Case-insensitive (database collation).</summary>
    Task<bool> NameExistsAsync(string name, int? excludeDepartmentId, CancellationToken cancellationToken);

    /// <summary>The department managed by this user (a user manages at most one), tracked. Null if none.</summary>
    Task<Department?> FindManagedByAsync(int userId, CancellationToken cancellationToken);

    Task<int> CountActiveUsersAsync(int departmentId, CancellationToken cancellationToken);

    /// <summary>Whether an active category routes tickets to this department (HandlingDepartmentId).</summary>
    Task<bool> IsHandlingDepartmentOfActiveCategoryAsync(int departmentId, CancellationToken cancellationToken);

    /// <summary>Stages a new department; written by <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
    void Add(Department department);
}
