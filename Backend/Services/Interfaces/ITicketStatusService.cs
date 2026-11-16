using Common.DTOs.Tickets;

namespace Services.Interfaces;

/// <summary>
/// Status changes of a single ticket – PATCH /api/tickets/{id}/status (spec 3.3, 9.2, task 3.5). The transition is the
/// pair (current status, new status) and the user's role on the ticket, by TicketStatusRules. The status, its history
/// row, the message to the employee and everything that goes with the transition are saved in one transaction with the
/// version the user saw (spec 11.2). Order of the checks: 400 → 404 → 403 → 409 → 422.
/// </summary>
public interface ITicketStatusService
{
    /// <exception cref="Common.Exceptions.ValidationException">Invalid request, a missing message, or a reason from the requester.</exception>
    /// <exception cref="Common.Exceptions.NotFoundException">Missing or outside the user's scope.</exception>
    /// <exception cref="Common.Exceptions.ForbiddenException">Neither the requester nor a handler in scope (a manager, the executive).</exception>
    /// <exception cref="Common.Exceptions.ConflictException">The ticket was changed since the user's version.</exception>
    /// <exception cref="Common.Exceptions.BusinessRuleException">
    /// The transition is not allowed (from this status, for this role), the ticket is another handler's, or the reopen
    /// window has passed.
    /// </exception>
    Task<TicketUpdatedDto> ChangeStatusAsync(int ticketId, ChangeTicketStatusRequest request,
        CancellationToken cancellationToken);
}

/// <summary>Closes resolved tickets 7 days after the resolution (spec 3.3, 10.2, 14.9) – run by AutoCloseResolvedWorker.</summary>
public interface IAutoCloseService
{
    /// <summary>
    /// Resolved tickets due for closing, of every organization (ids only), the oldest first. Call in a scope without an
    /// organization; close each one with <see cref="TryCloseAsync"/> in a scope of its own organization.
    /// </summary>
    Task<IReadOnlyList<TicketRef>> GetDueAsync(int batchSize, CancellationToken cancellationToken);

    /// <summary>
    /// Closes the ticket if it is still Resolved and due (a system transition). False when skipped: no longer resolved,
    /// not due, not found, or changed meanwhile by the employee (409 – the employee's action wins, quietly).
    /// </summary>
    Task<bool> TryCloseAsync(int ticketId, CancellationToken cancellationToken);
}
