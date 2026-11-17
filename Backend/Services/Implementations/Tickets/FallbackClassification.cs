using Common.DTOs.Routing;
using Common.Enums;
using Common.Models;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

namespace Services.Implementations.Tickets;

/// <summary>
/// The fallback path (spec 5.6): GENERAL (the organization's IsFallback category), source Fallback, Urgency null until a
/// handler sets it (decision in CLAUDE.md; the SLA then uses Medium). One place for a new ticket without AI (task 3.3) and
/// for the AI worker when the analysis fails or is not usable (task 4.3).
/// </summary>
internal static class FallbackClassification
{
    /// <returns>The routing decision, to log after the save; null if the ticket could not be routed.</returns>
    public static async Task<RoutingDecision?> ApplyAsync(IRoutingService routing, ITicketClassifier classifier,
        Ticket ticket, DateTime now, ILogger logger, CancellationToken cancellationToken)
    {
        var fallbackId = await routing.GetFallbackCategoryIdAsync(ticket.OrganizationId, cancellationToken);
        if (fallbackId is not { } categoryId)
        {
            // Cannot happen with valid data (one fallback per organization, never deactivated). The ticket is kept –
            // New, unrouted – rather than failing.
            logger.LogError("Organization {OrganizationId} has no active fallback category; ticket {TicketId} stays unrouted",
                ticket.OrganizationId, ticket.Id);
            return null;
        }

        return await classifier.ApplyAsync(ticket,
            new TicketClassification(categoryId, Urgency: null, ClassificationSource.Fallback), now, cancellationToken);
    }
}
