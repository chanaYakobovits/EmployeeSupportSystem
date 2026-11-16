using Common.Enums;

namespace Common.DTOs.Tickets;

// Bulk update – task 3.6 (spec 7.6, 9.2, 11.2): up to BulkLimits.MaxItems tickets, each updated on its own.

/// <summary>One selected ticket with the version the user saw (base64).</summary>
public sealed record BulkTicketItem(int? Id, byte[]? RowVersion);

/// <summary>
/// POST /api/tickets/bulk/status. Only "התחל טיפול" (InProgress): a status change that needs a message to the employee
/// (request info, resolve) is made one ticket at a time.
/// </summary>
public sealed record BulkChangeStatusRequest(TicketStatus? NewStatus, IReadOnlyList<BulkTicketItem>? Items);

/// <summary>
/// POST /api/tickets/bulk/assign. Exactly one target, as in POST /{id}/assign; one reason (required) for every ticket.
/// </summary>
public sealed record BulkAssignRequest(int? TargetUserId, int? TargetDepartmentId, string? Reason,
    IReadOnlyList<BulkTicketItem>? Items);

/// <summary>The result of a bulk update (spec 11.2): 200 even when some or all of the tickets failed.</summary>
/// <param name="Succeeded">The updated tickets, in the order of the request.</param>
/// <param name="Failed">The others, in the order of the request.</param>
public sealed record BulkUpdateResultDto(IReadOnlyList<int> Succeeded, IReadOnlyList<BulkFailureDto> Failed);

/// <param name="ErrorType">
/// The error type of the single action (e.g. "ticket-assigned-to-other"), so the screen can say exactly why; null for
/// NotFound – whether the ticket exists or is out of scope is not told.
/// </param>
public sealed record BulkFailureDto(int Id, BulkFailureReason Reason, string? ErrorType);

/// <summary>
/// GET /api/tickets/bulk/assign-options: the targets of the bulk transfer dialog – active handlers of the user's
/// department (not the user) and other active departments with an active handler. What depends on a single ticket
/// (its requester, its current handler, its handling department) is checked per ticket (InvalidTarget).
/// </summary>
public sealed record BulkAssignOptionsDto(IReadOnlyList<AssigneeOptionDto> Handlers,
    IReadOnlyList<NamedRefDto> Departments);
