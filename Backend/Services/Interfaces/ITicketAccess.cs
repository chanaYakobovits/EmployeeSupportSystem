using System.Linq.Expressions;
using Common.DTOs.Tickets;
using Common.Enums;
using Common.Models;
using Services.Implementations.Tickets;

namespace Services.Interfaces;

/// <summary>Who may see and act on a single ticket (spec 2.4). The one entry point of every single-ticket endpoint.</summary>
public interface ITicketAccess
{
    /// <summary>The ticket's facts and the signed-in user's permissions on it.</summary>
    /// <exception cref="Common.Exceptions.NotFoundException">Missing, another organization's, or outside every scope.</exception>
    /// <exception cref="Common.Exceptions.AuthenticationFailedException">The user is no longer active.</exception>
    Task<TicketAccessResult> GetAsync(int ticketId, CancellationToken cancellationToken);

    /// <summary>The tickets of a staff list the signed-in user may request (<see cref="TicketListScopes"/>).</summary>
    /// <exception cref="Common.Exceptions.ForbiddenException">The scope is not one of the user's (403).</exception>
    /// <exception cref="Common.Exceptions.AuthenticationFailedException">The user is no longer active.</exception>
    Task<Expression<Func<Ticket, bool>>> GetListScopeAsync(TicketListScope scope, CancellationToken cancellationToken);
}

/// <param name="UserId">The signed-in user.</param>
/// <param name="Viewer">The signed-in user as the policy saw them, to evaluate the ticket again after a change.</param>
public sealed record TicketAccessResult(TicketAccessFacts Ticket, TicketPermissions Permissions, int UserId,
    TicketViewer Viewer);
