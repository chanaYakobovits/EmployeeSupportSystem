using Common.DTOs.Paging;
using Common.Enums;

namespace Common.DTOs.Tickets;

/// <summary>GET /api/tickets – the staff lists (spec 7.6, 9.2, 9.4). Filters are combined with AND.</summary>
public sealed class StaffTicketsQuery : PageQuery
{
    /// <summary>Required. A scope the user may not request → 403.</summary>
    public TicketListScope? Scope { get; set; }

    /// <summary>
    /// Any of these statuses (?status=New&amp;status=Assigned). Empty → every status except
    /// <see cref="Constants.TicketStatuses.Final"/> (Closed, Cancelled); they are listed only when asked for here.
    /// </summary>
    public List<TicketStatus>? Status { get; set; }

    public int? CategoryId { get; set; }

    /// <summary>Any of these urgencies; NotSet matches tickets without one (?urgency=High&amp;urgency=NotSet).</summary>
    public List<UrgencyFilter>? Urgency { get; set; }

    /// <summary>Opened on or after this day (Israel time).</summary>
    public DateOnly? From { get; set; }

    /// <summary>Opened on or before this day, the whole day included (Israel time).</summary>
    public DateOnly? To { get; set; }

    /// <summary>Contained in the subject or the requester's name (LIKE, spec 8.4).</summary>
    public string? Search { get; set; }

    /// <summary>Only tickets past their due time while the SLA clock runs (<see cref="Constants.TicketStatuses.Open"/>).</summary>
    public bool OverdueOnly { get; set; }
}

/// <summary>Closed list of sortBy values for GET /api/tickets (spec 9.4).</summary>
public static class StaffTicketSortFields
{
    /// <summary>
    /// The default (decision in CLAUDE.md, instead of dueAt of spec 7.6): by what the ticket waits for – the handler
    /// (New, Assigned, InProgress), the employee, the confirmation of a resolution (Resolved), nothing (Closed,
    /// Cancelled). Waiting for the handler: overdue first, then the most urgent (none = Medium), then the nearest due
    /// time (none last). The other groups: the latest update first. Ties: the lowest id. sortDir is ignored.
    /// </summary>
    public const string Priority = "priority";
    /// <summary>Ascending: the nearest due time first. Tickets without a due time last.</summary>
    public const string DueAt = "dueAt";
    /// <summary>By severity; a ticket without urgency counts as Medium (as its SLA). Without sortDir: Critical first.</summary>
    public const string Urgency = "urgency";
    public const string CreatedAt = "createdAt";
    public const string UpdatedAt = "updatedAt";
    /// <summary>By the order of the workflow (New → … → Cancelled), not alphabetically.</summary>
    public const string Status = "status";

    public const string Default = Priority;

    /// <summary>
    /// The direction when sortDir is not sent: what the handler needs first – the nearest due time, the most urgent, the
    /// newest – and the workflow order for status. Priority has one fixed order.
    /// </summary>
    public static bool IsDescendingByDefault(string sortBy) => sortBy is Urgency or CreatedAt or UpdatedAt;

    public static readonly IReadOnlyList<string> All = [Priority, DueAt, Urgency, CreatedAt, UpdatedAt, Status];
}
