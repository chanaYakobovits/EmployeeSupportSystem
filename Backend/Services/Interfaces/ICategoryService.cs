using Common.DTOs.Categories;

namespace Services.Interfaces;

/// <summary>Categories and routing of the signed-in user's organization (spec 3.5, 7.8, 9.3). No delete – only deactivate.</summary>
public interface ICategoryService
{
    /// <summary>Any signed-in user. <paramref name="includeInactive"/> is honored for Admin only.</summary>
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.ValidationException">Invalid fields, or an inactive / unknown handling department.</exception>
    /// <exception cref="Common.Exceptions.ConflictException">The code is taken in the organization (→ 409).</exception>
    Task<CategorySaveResult> CreateAsync(CreateCategoryRequest request, string ipAddress, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.NotFoundException"/>
    /// <exception cref="Common.Exceptions.BusinessRuleException">Deactivating the fallback category (→ 422).</exception>
    Task<CategorySaveResult> UpdateAsync(int categoryId, UpdateCategoryRequest request, string ipAddress,
        CancellationToken cancellationToken);
}
