using Common.DTOs.Categories;
using Common.DTOs.Departments;
using Repositories.Interfaces;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>
/// The full lists of an organization's reference data (active and inactive) through <see cref="IReferenceDataCache"/>,
/// in one place: every reader must cache the same type under a key, or the entries would replace each other.
/// The organization is explicit (background jobs have no signed-in user); the repository reads within the scope's
/// organization filter.
/// </summary>
public static class ReferenceDataLookups
{
    public static Task<IReadOnlyList<DepartmentDto>> DepartmentsAsync(IReferenceDataCache cache,
        IDepartmentRepository departments, int organizationId, CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(ReferenceData.Departments, organizationId,
            ct => departments.GetAllAsync(includeInactive: true, ct), cancellationToken);

    public static Task<IReadOnlyList<CategoryDto>> CategoriesAsync(IReferenceDataCache cache,
        ICategoryRepository categories, int organizationId, CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(ReferenceData.Categories, organizationId, categories.GetAllAsync, cancellationToken);
}
