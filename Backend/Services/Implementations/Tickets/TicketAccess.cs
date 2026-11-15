using Common.DTOs.Tickets;
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

    public async Task<TicketAccessResult> GetAsync(int ticketId, CancellationToken cancellationToken)
    {
        // From the database, like the department of a new ticket: the token may be up to 15 minutes old.
        var viewer = await users.GetTicketViewerAsync(currentUser.UserId, cancellationToken)
                     ?? throw new AuthenticationFailedException(AuthenticationFailedException.NotSignedInTitle,
                         errorType: "unauthorized");

        // The organization query filter applies: another organization's ticket is not found either.
        var facts = await tickets.GetAccessFactsAsync(ticketId, cancellationToken)
                    ?? throw new NotFoundException(TicketNotFoundTitle);

        var permissions = TicketAccessPolicy.Evaluate(viewer, facts);
        if (!permissions.CanView)
        {
            // Spec 2.4: 404, not 403 – the user does not learn that the ticket exists.
            throw new NotFoundException(TicketNotFoundTitle);
        }

        return new TicketAccessResult(facts, permissions, viewer.UserId);
    }
}
