using Common.DTOs.Routing;
using Common.Enums;
using Common.Models;
using Microsoft.Extensions.Logging;
using Repositories.Interfaces;
using Services.Implementations.Routing;
using Services.Interfaces;

namespace Services.Implementations.Tickets;

/// <inheritdoc />
public sealed class TicketClassifier(
    IRoutingService routing,
    IReferenceDataCache cache,
    ISlaPolicyRepository slaPolicies) : ITicketClassifier
{
    public async Task<RoutingDecision?> ApplyAsync(Ticket ticket, TicketClassification classification, DateTime now,
        CancellationToken cancellationToken)
    {
        RoutingDecision? decision = null;
        if (ticket.CategoryId != classification.CategoryId || ticket.AssignedDepartmentId is null)
        {
            decision = await routing.RouteAsync(ticket.OrganizationId, classification.CategoryId, ticket.RequesterId,
                ticket.RequesterDepartmentId, cancellationToken);
            if (ticket.AssignedDepartmentId == decision.DepartmentId)
            {
                // The new category is handled by the same department: the ticket stays with whoever has it (its handler,
                // or the queue) rather than moving to the least loaded colleague.
                decision = new RoutingDecision(decision.DepartmentId, ticket.AssignedToUserId,
                    RoutingReason.SameDepartmentKeptHandler);
            }

            ticket.AssignedDepartmentId = decision.DepartmentId;
            ticket.AssignedToUserId = decision.HandlerUserId;
        }

        // CategoryId and ClassificationSource are set together (decision in CLAUDE.md); Urgency with them, null only
        // on the fallback path.
        ticket.CategoryId = classification.CategoryId;
        ticket.Urgency = classification.Urgency;
        ticket.ClassificationSource = classification.Source;

        // From the opening (spec 3.4). Task 3.5 adds the time spent waiting for the employee.
        var hours = await SlaCalculator.GetHoursAsync(cache, slaPolicies, ticket.OrganizationId, cancellationToken);
        ticket.DueAt = SlaCalculator.DueAt(ticket.CreatedAt, ticket.Urgency, hours);

        if (ticket.Status == TicketStatus.New)
        {
            // Spec 3.3: New → Assigned is automatic, by the system, after the analysis or the fallback.
            ticket.StatusHistory.Add(new TicketStatusHistory
            {
                PreviousStatus = TicketStatus.New,
                NewStatus = TicketStatus.Assigned,
                ChangedByUserId = null,
                ChangedAt = now,
            });
            ticket.Status = TicketStatus.Assigned;
        }

        ticket.UpdatedAt = now;
        return decision;
    }
}

/// <summary>The routing log line (spec 11.4: routing with its source), ids and reasons only – never content (spec 6.4).</summary>
public static class RoutingLog
{
    public static void Routed(ILogger logger, int ticketId, RoutingDecision decision, ClassificationSource source) =>
        logger.LogInformation(
            "Ticket {TicketId} routed to department {DepartmentId}, handler {HandlerUserId} (reason {RoutingReason}, source {ClassificationSource})",
            ticketId, decision.DepartmentId, decision.HandlerUserId, decision.Reason, source);
}
