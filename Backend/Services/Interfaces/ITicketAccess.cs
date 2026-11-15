using Common.DTOs.Tickets;
using Services.Implementations.Tickets;

namespace Services.Interfaces;

/// <summary>Who may see and act on a single ticket (spec 2.4). The one entry point of every single-ticket endpoint.</summary>
public interface ITicketAccess
{
    /// <summary>The ticket's facts and the signed-in user's permissions on it.</summary>
    /// <exception cref="Common.Exceptions.NotFoundException">Missing, another organization's, or outside every scope.</exception>
    /// <exception cref="Common.Exceptions.AuthenticationFailedException">The user is no longer active.</exception>
    Task<TicketAccessResult> GetAsync(int ticketId, CancellationToken cancellationToken);
}

/// <param name="UserId">The signed-in user.</param>
public sealed record TicketAccessResult(TicketAccessFacts Ticket, TicketPermissions Permissions, int UserId);
