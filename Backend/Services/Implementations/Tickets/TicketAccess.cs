using System.Linq.Expressions;
using Common.DTOs.Tickets;
using Common.Enums;
using Common.Models;
using Common.Exceptions;
using Common.Interfaces;
using Repositories.Interfaces;
using Services.Interfaces;

namespace Services.Implementations.Tickets;

/// <summary>
/// Loads the signed-in user and the ticket's routing columns (two small queries, no content) and applies
/// <see cref="TicketAccessPolicy"/>. Every endpoint of a single ticket starts here.
/// </summary>
public sealed class TicketAccess(ITicketRepository tickets, IUserRepository users, ICurrentUser currentUser) : ITicketAccess
{
    public const string TicketNotFoundTitle = "הפנייה לא נמצאה";

    public const string ScopeNotAllowedTitle = "אין לך הרשאה לצפות ברשימה זו";

    public async Task<TicketAccessResult> GetAsync(int ticketId, CancellationToken cancellationToken)
    {
        var viewer = await GetViewerAsync(cancellationToken);

        // The organization query filter applies: another organization's ticket is not found either.
        var facts = await tickets.GetAccessFactsAsync(ticketId, cancellationToken)
                    ?? throw new NotFoundException(TicketNotFoundTitle);

        var permissions = TicketAccessPolicy.Evaluate(viewer, facts);
        if (!permissions.CanView)
        {
            // Spec 2.4: 404, not 403 – the user does not learn that the ticket exists.
            throw new NotFoundException(TicketNotFoundTitle);
        }

        return new TicketAccessResult(facts, permissions, viewer.UserId, viewer);
    }

    public async Task<Expression<Func<Ticket, bool>>> GetListScopeAsync(TicketListScope scope,
        CancellationToken cancellationToken)
    {
        var viewer = await GetViewerAsync(cancellationToken);

        // A list is not a single record: asking for one outside the user's roles is 403, not 404 (spec 9.5).
        if (!TicketListScopes.IsAllowed(viewer, scope))
        {
            throw new ForbiddenException(ScopeNotAllowedTitle, errorType: "ticket-scope-not-allowed");
        }

        return TicketListScopes.Predicate(viewer, scope);
    }

    // From the database, like the department of a new ticket: the token may be up to 15 minutes old.
    private async Task<TicketViewer> GetViewerAsync(CancellationToken cancellationToken) =>
        await users.GetTicketViewerAsync(currentUser.UserId, cancellationToken)
        ?? throw new AuthenticationFailedException(AuthenticationFailedException.NotSignedInTitle,
            errorType: "unauthorized");
}
