using Common.DTOs.Paging;
using Common.Enums;

namespace Common.DTOs.Tickets;

/// <summary>GET /api/tickets/my – the signed-in user's own tickets (spec 7.3, 9.4). Filters are combined with AND.</summary>
public sealed class MyTicketsQuery : PageQuery
{
    /// <summary>Any of these statuses (?status=New&amp;status=Assigned). Empty → all statuses.</summary>
    public List<TicketStatus>? Status { get; set; }

    /// <summary>Contained in the subject (LIKE, spec 8.4).</summary>
    public string? Search { get; set; }
}

/// <summary>Closed list of sortBy values for GET /api/tickets/my (spec 9.4).</summary>
public static class MyTicketSortFields
{
    /// <summary>The default, descending: the newest tickets first.</summary>
    public const string CreatedAt = "createdAt";
    public const string UpdatedAt = "updatedAt";
    /// <summary>By the order of the workflow (New → … → Cancelled), not alphabetically.</summary>
    public const string Status = "status";

    public const string Default = CreatedAt;

    /// <summary>The direction when sortDir is not sent: the default column sorts descending, any other column ascending.</summary>
    public static bool IsDescendingByDefault(string sortBy) => sortBy == Default;

    public static readonly IReadOnlyList<string> All = [CreatedAt, UpdatedAt, Status];
}
