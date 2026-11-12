using System.Linq.Expressions;
using Common.DTOs.Paging;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Implementations;

/// <summary>Sorting and paging in the SQL query (spec 9.4) – never in memory.</summary>
internal static class QueryableExtensions
{
    public static IOrderedQueryable<T> OrderByDirection<T, TKey>(this IQueryable<T> source,
        Expression<Func<T, TKey>> keySelector, bool descending) =>
        descending ? source.OrderByDescending(keySelector) : source.OrderBy(keySelector);

    public static IOrderedQueryable<T> ThenByDirection<T, TKey>(this IOrderedQueryable<T> source,
        Expression<Func<T, TKey>> keySelector, bool descending) =>
        descending ? source.ThenByDescending(keySelector) : source.ThenBy(keySelector);

    /// <summary>
    /// COUNT of the filtered query, then the requested page of the ordered query projected to DTOs.
    /// <paramref name="ordered"/> must be <paramref name="filtered"/> with a total (unique) order, so pages are stable.
    /// </summary>
    public static async Task<PagedResult<TDto>> ToPagedResultAsync<T, TDto>(this IQueryable<T> filtered,
        IOrderedQueryable<T> ordered, Expression<Func<T, TDto>> projection, PageQuery page,
        CancellationToken cancellationToken)
    {
        var totalCount = await filtered.CountAsync(cancellationToken);
        var items = totalCount == 0
            ? []
            : await ordered
                .Skip((page.Page - 1) * page.PageSize)
                .Take(page.PageSize)
                .Select(projection)
                .ToListAsync(cancellationToken);

        return new PagedResult<TDto>(items, page.Page, page.PageSize, totalCount);
    }
}
