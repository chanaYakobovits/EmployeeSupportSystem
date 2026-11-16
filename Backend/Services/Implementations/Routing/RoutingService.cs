using Common.DTOs.Categories;
using Common.DTOs.Routing;
using Common.Enums;
using Repositories.Interfaces;
using Services.Interfaces;

namespace Services.Implementations.Routing;

/// <summary>
/// Loads what <see cref="RoutingRules"/> needs and lets it decide. Categories and departments (with HrUserId) from the
/// reference-data cache (spec 11.3); handlers, their activity and load always from the database, in one query.
/// "Central HR" is the handling department of the fallback category – never found by name.
/// </summary>
public sealed class RoutingService(
    IReferenceDataCache cache,
    ICategoryRepository categories,
    IDepartmentRepository departments,
    IUserRepository users) : IRoutingService
{
    public async Task<int?> GetFallbackCategoryIdAsync(int organizationId, CancellationToken cancellationToken) =>
        FindFallback(await ReferenceDataLookups.CategoriesAsync(cache, categories, organizationId, cancellationToken))?.Id;

    public async Task<RoutingDecision> RouteAsync(int organizationId, int categoryId, int requesterId,
        int requesterDepartmentId, CancellationToken cancellationToken)
    {
        var allCategories = await ReferenceDataLookups.CategoriesAsync(cache, categories, organizationId, cancellationToken);
        var category = allCategories.FirstOrDefault(c => c.Id == categoryId)
                       ?? throw new InvalidOperationException($"Category {categoryId} does not exist.");
        var centralDepartmentId = FindFallback(allCategories)?.HandlingDepartment?.Id
                                  ?? throw new InvalidOperationException("The organization has no fallback category with a handling department.");

        int? hrUserId = null;
        var departmentIds = new List<int> { centralDepartmentId };
        if (category.RoutingType == RoutingType.RequesterDepartmentHr)
        {
            var allDepartments = await ReferenceDataLookups.DepartmentsAsync(cache, departments, organizationId,
                cancellationToken);
            hrUserId = allDepartments.FirstOrDefault(d => d.Id == requesterDepartmentId)?.HrUser?.Id;
        }
        else if (category.HandlingDepartment is { } handling && handling.Id != centralDepartmentId)
        {
            departmentIds.Add(handling.Id);
        }

        var candidates = await users.GetHandlerCandidatesAsync(departmentIds, hrUserId, cancellationToken);
        return RoutingRules.Decide(new RoutingInput(category.RoutingType, category.HandlingDepartment?.Id, hrUserId,
            centralDepartmentId, requesterId, candidates));
    }

    // Exactly one fallback per organization (unique filtered index), and it cannot be deactivated.
    private static CategoryDto? FindFallback(IReadOnlyList<CategoryDto> all) =>
        all.FirstOrDefault(c => c.IsFallback && c.IsActive);
}
