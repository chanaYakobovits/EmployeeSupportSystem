using Common.DTOs.Auth;
using Common.DTOs.Categories;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Repositories.Data;
using Repositories.Interfaces;

namespace Repositories.Implementations;

public sealed class CategoryRepository(AppDbContext db) : ICategoryRepository
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken) =>
        // Ordered before the projection: EF cannot translate a member of a constructor-built DTO.
        await Project(db.Categories.AsNoTracking().OrderBy(c => c.IsFallback).ThenBy(c => c.Name))
            .ToListAsync(cancellationToken);

    public Task<CategoryDto?> GetDtoAsync(int categoryId, CancellationToken cancellationToken) =>
        Project(db.Categories.AsNoTracking().Where(c => c.Id == categoryId))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Category?> FindForUpdateAsync(int categoryId, CancellationToken cancellationToken) =>
        db.Categories.SingleOrDefaultAsync(c => c.Id == categoryId, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken) =>
        db.Categories.AnyAsync(c => c.Code == code, cancellationToken);

    public void Add(Category category) => db.Categories.Add(category);

    private static IQueryable<CategoryDto> Project(IQueryable<Category> categories) =>
        categories.Select(c => new CategoryDto(
            c.Id,
            c.Code,
            c.Name,
            c.Description,
            c.RoutingType,
            c.HandlingDepartment == null
                ? null
                : new DepartmentSummaryDto(c.HandlingDepartment.Id, c.HandlingDepartment.Name),
            c.IsFallback,
            c.IsActive));
}
