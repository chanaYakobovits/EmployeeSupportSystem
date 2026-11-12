namespace Repositories.Interfaces;

/// <summary>Tickets of the signed-in user's organization. Only what task 2.3 needs so far; the rest comes in stage 3.</summary>
public interface ITicketRepository
{
    /// <summary>
    /// Returns the handler's unresolved tickets (<see cref="Common.Constants.TicketStatuses.Unresolved"/>) to the queue of
    /// their handling department: AssignedToUserId = null, UpdatedAt = now. Status unchanged.
    /// Executes immediately (bulk UPDATE, not SaveChanges): call it inside
    /// <see cref="IUnitOfWork.ExecuteInTransactionAsync{T}"/> together with the deactivation.
    /// </summary>
    /// <returns>Number of tickets returned to the queue.</returns>
    Task<int> UnassignUnresolvedTicketsAsync(int handlerUserId, DateTime now, CancellationToken cancellationToken);
}
