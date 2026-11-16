using Common.DTOs.Tickets;

namespace Services.Interfaces;

/// <summary>
/// Staff handling of a single ticket – task 3.4: reclassification and transfer (spec 4.4, 9.2). Every change carries the
/// version the user saw (spec 11.2) and is recorded with its reason in the staff timeline only.
/// Order of the checks: 400 → 404 → 403 → 409 → 422.
/// </summary>
public interface ITicketHandlingService
{
    /// <summary>
    /// Changes the category and/or urgency (ClassificationSource Manual) through <see cref="ITicketClassifier"/>: a new
    /// category routes the ticket again (another department and handler); the due time is recalculated from the opening.
    /// </summary>
    /// <exception cref="Common.Exceptions.ValidationException">Invalid request, or an inactive / unknown category.</exception>
    /// <exception cref="Common.Exceptions.NotFoundException">Missing or outside the user's scope.</exception>
    /// <exception cref="Common.Exceptions.ForbiddenException">Not a handler in scope or the handling department's manager.</exception>
    /// <exception cref="Common.Exceptions.ConflictException">The ticket was changed since the user's version.</exception>
    /// <exception cref="Common.Exceptions.BusinessRuleException">Resolved / closed / cancelled, or nothing changes.</exception>
    Task<TicketUpdatedDto> ReclassifyAsync(int ticketId, ReclassifyTicketRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Assigns the ticket to an active handler of its handling department (the user themselves = "קח לטיפולי"), or moves
    /// it to another department's queue (handlers only).
    /// </summary>
    /// <exception cref="Common.Exceptions.ValidationException">Invalid request, a missing reason, or an inactive department.</exception>
    /// <exception cref="Common.Exceptions.NotFoundException">Missing or outside the user's scope.</exception>
    /// <exception cref="Common.Exceptions.ForbiddenException">The user may not make this kind of assignment.</exception>
    /// <exception cref="Common.Exceptions.ConflictException">The ticket was changed since the user's version.</exception>
    /// <exception cref="Common.Exceptions.BusinessRuleException">An invalid target, a final status, or nothing changes.</exception>
    Task<TicketUpdatedDto> AssignAsync(int ticketId, AssignTicketRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// The targets of the transfer dialog: active handlers of the handling department (not the requester, not the current
    /// handler) and – for handlers who may move the ticket – other departments with an active handler.
    /// </summary>
    /// <exception cref="Common.Exceptions.NotFoundException">Missing or outside the user's scope.</exception>
    /// <exception cref="Common.Exceptions.ForbiddenException">The user may not assign this ticket.</exception>
    Task<TicketAssignOptionsDto> GetAssignOptionsAsync(int ticketId, CancellationToken cancellationToken);

    /// <summary>
    /// The targets of the bulk transfer dialog (task 3.6): active handlers of the user's department (not the user) and
    /// other departments with an active handler – the same lists as <see cref="GetAssignOptionsAsync"/>, for the
    /// department instead of one ticket.
    /// </summary>
    /// <exception cref="Common.Exceptions.ForbiddenException">The user is not a handler.</exception>
    Task<BulkAssignOptionsDto> GetBulkAssignOptionsAsync(CancellationToken cancellationToken);
}
