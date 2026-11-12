using Common.Constants;
using Microsoft.EntityFrameworkCore;
using Repositories.Data;
using Repositories.Interfaces;

namespace Repositories.Implementations;

public sealed class TicketRepository(AppDbContext db) : ITicketRepository
{
    // The organization query filter applies to ExecuteUpdate too. RowVersion changes automatically (rowversion column),
    // so a handler who still has one of these tickets open gets 409 on the next update.
    public Task<int> UnassignUnresolvedTicketsAsync(int handlerUserId, DateTime now, CancellationToken cancellationToken) =>
        db.Tickets
            .Where(t => t.AssignedToUserId == handlerUserId && TicketStatuses.Unresolved.Contains(t.Status))
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.AssignedToUserId, (int?)null)
                    .SetProperty(t => t.UpdatedAt, now),
                cancellationToken);
}
