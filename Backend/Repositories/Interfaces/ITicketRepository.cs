using System.Linq.Expressions;
using Common.DTOs.Paging;
using Common.DTOs.Tickets;
using Common.Enums;
using Common.Models;

namespace Repositories.Interfaces;

/// <summary>Tickets of the signed-in user's organization (the organization query filter applies to every method).</summary>
public interface ITicketRepository
{
    /// <summary>
    /// Stages a new ticket together with its child rows (status history; later the AI job). Written by
    /// <see cref="IUnitOfWork.SaveChangesAsync"/> in one transaction.
    /// </summary>
    void Add(Ticket ticket);

    // ---- the requester's view (task 3.1) ----

    /// <summary>
    /// The requester's own tickets, filtered, sorted and paged in SQL. The query must already be validated
    /// (page, pageSize, sortBy, statuses).
    /// </summary>
    Task<PagedResult<RequesterTicketListItemDto>> GetRequesterPageAsync(int requesterId, MyTicketsQuery query,
        CancellationToken cancellationToken);

    /// <summary>The summary cards, in one grouped query (no rows are loaded).</summary>
    /// <param name="monthStartUtc">Start of the current month (Israel time) in UTC, for "closed this month".</param>
    Task<RequesterTicketsSummaryDto> GetRequesterSummaryAsync(int requesterId, DateTime monthStartUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// The ticket with its status timeline (no reasons), or null. Does not check the viewing scope: call it only after
    /// TicketAccess allowed the ticket.
    /// </summary>
    Task<RequesterTicketDetailsDto?> GetDetailsAsync(int ticketId, CancellationToken cancellationToken);

    // ---- access (task 3.2) ----

    /// <summary>The columns that decide who may see and act on the ticket (one row, no content). Null if not found.</summary>
    Task<TicketAccessFacts?> GetAccessFactsAsync(int ticketId, CancellationToken cancellationToken);

    /// <summary>Tracked ticket (no navigations), to update it with SaveChanges – RowVersion is its concurrency token.</summary>
    Task<Ticket?> FindForUpdateAsync(int ticketId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the tracked <paramref name="ticket"/> only if the database still has <paramref name="rowVersion"/> – the
    /// version the client saw (spec 11.2). Otherwise SaveChanges throws ConflictException (409).
    /// </summary>
    void ExpectRowVersion(Ticket ticket, byte[] rowVersion);

    // ---- the staff lists and view (task 3.4) ----

    /// <summary>
    /// A staff list: the tickets matching <paramref name="scope"/> (built by the service from the permission matrix),
    /// filtered, sorted and paged in SQL. The query must already be validated.
    /// </summary>
    /// <param name="fromUtc">Opened at or after (inclusive).</param>
    /// <param name="toUtc">Opened before (exclusive).</param>
    /// <param name="now">For the overdue filter and column.</param>
    Task<PagedResult<StaffTicketListItemDto>> GetStaffPageAsync(Expression<Func<Ticket, bool>> scope,
        StaffTicketsQuery query, DateTime? fromUtc, DateTime? toUtc, DateTime now, CancellationToken cancellationToken);

    /// <summary>
    /// The staff view of the ticket, or null. Does not check the viewing scope: call it only after TicketAccess allowed
    /// the ticket, with the actions it allows.
    /// </summary>
    Task<StaffTicketDetailsDto?> GetStaffDetailsAsync(int ticketId, IReadOnlyList<string> allowedActions, DateTime now,
        CancellationToken cancellationToken);

    // ---- user administration (task 2.3) ----

    /// <summary>
    /// Returns the handler's unresolved tickets (<see cref="Common.Constants.TicketStatuses.Unresolved"/>) to the queue of
    /// their handling department: AssignedToUserId = null, UpdatedAt = now. Those in <paramref name="demoteFrom"/> move to
    /// <paramref name="demoteTo"/> (InProgress → Assigned: nobody is handling them now – task 3.5), each with a system
    /// history row staged for the caller's SaveChanges; any other status is unchanged.
    /// The UPDATE executes immediately (bulk, not SaveChanges): call it inside
    /// <see cref="IUnitOfWork.ExecuteInTransactionAsync{T}"/> together with the deactivation and its SaveChanges.
    /// </summary>
    /// <returns>Number of tickets returned to the queue.</returns>
    Task<int> UnassignUnresolvedTicketsAsync(int handlerUserId, TicketStatus demoteFrom, TicketStatus demoteTo,
        DateTime now, CancellationToken cancellationToken);

    // ---- auto-close (task 3.5, spec 10.2) ----

    /// <summary>
    /// Resolved tickets with ResolvedAt at or before <paramref name="resolvedAtOrBefore"/>, of <b>every</b> organization
    /// (the background job has no organization; ids only – each ticket is then closed in a scope of its own
    /// organization). The oldest first, at most <paramref name="take"/>.
    /// </summary>
    Task<IReadOnlyList<TicketRef>> GetResolvedDueAsync(DateTime resolvedAtOrBefore, int take,
        CancellationToken cancellationToken);
}
