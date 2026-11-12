namespace Common.DTOs.Paging;

/// <summary>
/// Paging and sorting parameters of a list endpoint (spec 9.4), bound from the query string.
/// Validated by the service (page ≥ 1, pageSize 1..100, sortBy from the list's closed set, sortDir asc|desc).
/// </summary>
public abstract class PageQuery
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;
    public const string Ascending = "asc";
    public const string Descending = "desc";

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = DefaultPageSize;

    /// <summary>Null → the list's default order.</summary>
    public string? SortBy { get; set; }

    /// <summary>"asc" (default) or "desc", case-insensitive.</summary>
    public string? SortDir { get; set; }

    public bool IsDescending => string.Equals(SortDir, Descending, StringComparison.OrdinalIgnoreCase);
}
