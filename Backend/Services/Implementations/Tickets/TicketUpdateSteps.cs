using Common.DTOs.Tickets;
using Common.Exceptions;
using Common.Models;
using Microsoft.Extensions.Logging;
using Repositories.Interfaces;
using Services.Interfaces;

namespace Services.Implementations.Tickets;

/// <summary>
/// The steps every change of a ticket by staff or its requester shares (tasks 3.4, 3.5; spec 11.2): the tracked row with
/// the version the user saw (409 if it changed), one SaveChanges (409 if it changed meanwhile), and the response.
/// Logs ids and the action only.
/// </summary>
internal sealed class TicketUpdateSteps(ITicketRepository tickets, IUnitOfWork unitOfWork, ILogger logger)
{
    public const string ConflictTitle = "הפנייה עודכנה על ידי משתמש אחר";
    public const string ConflictDetail = "יש לרענן את הפנייה ולנסות שוב.";

    /// <summary>
    /// The tracked ticket, after the scope check (<paramref name="access"/>). The version is compared now, before the
    /// rules run against a state the user has not seen; and again by the UPDATE itself.
    /// </summary>
    public async Task<Ticket> LoadAsync(TicketAccessResult access, byte[] rowVersion, CancellationToken cancellationToken)
    {
        var ticket = await tickets.FindForUpdateAsync(access.Ticket.TicketId, cancellationToken)
                     ?? throw new NotFoundException(TicketAccess.TicketNotFoundTitle);

        if (!ticket.RowVersion.AsSpan().SequenceEqual(rowVersion))
        {
            logger.LogWarning("Concurrency conflict on ticket {TicketId} by user {UserId} (stale version)",
                ticket.Id, access.UserId);
            throw Conflict();
        }

        tickets.ExpectRowVersion(ticket, rowVersion);
        return ticket;
    }

    /// <summary>The ticket and everything staged with it – one transaction.</summary>
    public async Task SaveAsync(Ticket ticket, string action, int userId, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictException exception) when (exception.ErrorType == "concurrency")
        {
            logger.LogWarning("Concurrency conflict on ticket {TicketId} ({Action}) by user {UserId}",
                ticket.Id, action, userId);
            throw Conflict();
        }
    }

    /// <summary>The new version, and whether the user still has the ticket in scope after the change.</summary>
    public static TicketUpdatedDto Result(Ticket ticket, TicketAccessResult access)
    {
        var facts = new TicketAccessFacts(ticket.Id, ticket.RequesterId, ticket.RequesterDepartmentId,
            ticket.AssignedDepartmentId, ticket.AssignedToUserId, ticket.Status, ticket.ResolvedAt);
        return new TicketUpdatedDto(ticket.Id, ticket.RowVersion,
            TicketAccessPolicy.Evaluate(access.Viewer, facts).CanView);
    }

    // Spec 9.5: the ticket's own wording, so the screen can show it as is.
    public static ConflictException Conflict() => new(ConflictTitle, ConflictDetail, "concurrency");
}
