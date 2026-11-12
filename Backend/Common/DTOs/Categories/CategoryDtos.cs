using Common.DTOs.Auth;
using Common.Enums;

namespace Common.DTOs.Categories;

/// <summary>GET /api/categories (spec 8.2). <see cref="HandlingDepartment"/> is null for RequesterDepartmentHr.</summary>
public sealed record CategoryDto(
    int Id,
    string Code,
    string Name,
    string Description,
    RoutingType RoutingType,
    DepartmentSummaryDto? HandlingDepartment,
    bool IsFallback,
    bool IsActive);

/// <summary>The fields that create and update share (validated by one set of rules).</summary>
public interface ICategoryFields
{
    string Name { get; }
    string Description { get; }
    RoutingType? RoutingType { get; }
    int? HandlingDepartmentId { get; }
}

/// <summary>
/// POST /api/categories. A new category is always active and never the fallback (there is exactly one, GENERAL).
/// </summary>
public sealed record CreateCategoryRequest(
    string Code,
    string Name,
    string Description,
    RoutingType? RoutingType,
    int? HandlingDepartmentId) : ICategoryFields;

/// <summary>
/// PUT /api/categories/{id}. No Code (fixed at creation) and no IsFallback (cannot be moved or removed).
/// </summary>
public sealed record UpdateCategoryRequest(
    string Name,
    string Description,
    RoutingType? RoutingType,
    int? HandlingDepartmentId,
    bool? IsActive) : ICategoryFields;

/// <summary>
/// Response of POST / PUT: the saved category plus warnings that did not block the save
/// (e.g. the handling department has no active handlers → tickets go to central HR, spec 3.5).
/// </summary>
public sealed record CategorySaveResult(CategoryDto Category, IReadOnlyList<string> Warnings);
