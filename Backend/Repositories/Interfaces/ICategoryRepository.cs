using Common.DTOs.Categories;
using Common.Models;

namespace Repositories.Interfaces;

/// <summary>Categories of the signed-in user's organization (organization query filter).</summary>
public interface ICategoryRepository
{
    /// <summary>Active and inactive. Ordered by name, the fallback category last.</summary>
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<CategoryDto?> GetDtoAsync(int categoryId, CancellationToken cancellationToken);

    /// <summary>Tracked. Null if not found.</summary>
    Task<Category?> FindForUpdateAsync(int categoryId, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken);

    /// <summary>Stages a new category; written by <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
    void Add(Category category);
}
