using Common.DTOs.Departments;

namespace Services.Interfaces;

/// <summary>Departments of the signed-in user's organization (spec 7.8, 9.3).</summary>
public interface IDepartmentService
{
    /// <summary>Any signed-in user. <paramref name="includeInactive"/> is honored for Admin only.</summary>
    Task<IReadOnlyList<DepartmentDto>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.ConflictException">The name is taken in the organization (→ 409).</exception>
    Task<DepartmentDto> CreateAsync(CreateDepartmentRequest request, string ipAddress, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.NotFoundException"/>
    /// <exception cref="Common.Exceptions.BusinessRuleException">
    /// Deactivating a department with active users or one that handles an active category; a manager who already
    /// manages another department (→ 422).
    /// </exception>
    Task<DepartmentDto> UpdateAsync(int departmentId, UpdateDepartmentRequest request, string ipAddress,
        CancellationToken cancellationToken);
}
