using Common.DTOs.Routing;
using Common.Models;

namespace Services.Interfaces;

/// <summary>
/// Applies a classification (category + urgency + source) to a ticket and routes it – the one entry point for the
/// fallback of a new ticket (task 3.3), the AI result (4.3) and a handler's reclassification or urgency change (3.4).
/// </summary>
public interface ITicketClassifier
{
    /// <summary>
    /// On a new or tracked ticket, without saving (the caller saves, in one transaction with its other changes):
    /// <list type="bullet">
    /// <item>CategoryId, Urgency, ClassificationSource, UpdatedAt.</item>
    /// <item>A new category, or a ticket never routed → routed again: AssignedDepartmentId and AssignedToUserId.
    /// A new category of the department that already handles the ticket → the assignment stays (task 3.4).
    /// Only the urgency changed → the assignment stays.</item>
    /// <item>DueAt = CreatedAt + the urgency's hours (no urgency → Medium), always recalculated (spec 3.1).</item>
    /// <item>Status New → Assigned, with a system row (ChangedByUserId null) in the status history (spec 3.3).
    /// Any other status is left alone: what a transfer does to it is decided by the caller (task 3.4).</item>
    /// </list>
    /// </summary>
    /// <returns>The routing decision, for the caller to log once the ticket has an id; null if it was not routed again.</returns>
    Task<RoutingDecision?> ApplyAsync(Ticket ticket, TicketClassification classification, DateTime now,
        CancellationToken cancellationToken);
}
