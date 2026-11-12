namespace Common.DTOs.Paging;

/// <summary>One page of a list (spec 9.4). <see cref="TotalCount"/> counts all matching rows, not just this page.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
