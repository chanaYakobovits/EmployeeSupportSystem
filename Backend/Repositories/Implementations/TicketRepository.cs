using Common.Constants;
using Common.DTOs.Paging;
using Common.DTOs.Tickets;
using Common.Enums;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Repositories.Data;
using Repositories.Interfaces;

namespace Repositories.Implementations;

public sealed class TicketRepository(AppDbContext db) : ITicketRepository
{
    public void Add(Ticket ticket) => db.Tickets.Add(ticket);

    // ===================== The requester's view (task 3.1) =====================

    public Task<PagedResult<RequesterTicketListItemDto>> GetRequesterPageAsync(int requesterId, MyTicketsQuery query,
        CancellationToken cancellationToken)
    {
        // Index Tickets(RequesterId, CreatedAt DESC) – spec 8.4.
        var tickets = db.Tickets.AsNoTracking().Where(t => t.RequesterId == requesterId);

        if (query.Status is { Count: > 0 } statuses)
        {
            tickets = tickets.Where(t => statuses.Contains(t.Status));
        }

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            // LIKE on the subject (spec 8.4), narrowed by the requester filter above.
            tickets = tickets.Where(t => t.Subject != null && t.Subject.Contains(search));
        }

        var sortBy = query.SortBy ?? MyTicketSortFields.Default;
        var desc = query.SortDir is null ? MyTicketSortFields.IsDescendingByDefault(sortBy) : query.IsDescending;
        var ordered = sortBy switch
        {
            MyTicketSortFields.UpdatedAt => tickets.OrderByDirection(t => t.UpdatedAt, desc),
            MyTicketSortFields.Status => tickets.OrderByDirection(t => t.Status, desc),
            _ => tickets.OrderByDirection(t => t.CreatedAt, desc),
        };
        // Unique tie-breaker, so pages are stable: the newer ticket first.
        ordered = ordered.ThenByDescending(t => t.Id);

        const int excerpt = TicketFieldLimits.ExcerptLength;
        return tickets.ToPagedResultAsync(ordered, t => new RequesterTicketListItemDto(
            t.Id,
            t.Subject ?? (t.Description.Length > excerpt ? t.Description.Substring(0, excerpt) : t.Description),
            t.Subject == null,
            t.Category != null ? t.Category.Name : null,
            t.Status,
            t.AssignedToUser != null ? t.AssignedToUser.FirstName + " " + t.AssignedToUser.LastName : null,
            t.CreatedAt,
            t.UpdatedAt), query, cancellationToken);
    }

    public async Task<RequesterTicketsSummaryDto> GetRequesterSummaryAsync(int requesterId, DateTime monthStartUtc,
        CancellationToken cancellationToken)
    {
        var open = TicketStatuses.RequesterOpen;

        // One row: SELECT COUNT(CASE ...), ... GROUP BY a constant. No row at all when the user has no tickets.
        var summary = await db.Tickets
            .AsNoTracking()
            .Where(t => t.RequesterId == requesterId)
            .GroupBy(_ => 1)
            .Select(g => new RequesterTicketsSummaryDto(
                g.Count(t => open.Contains(t.Status)),
                g.Count(t => t.Status == TicketStatus.WaitingForEmployee),
                g.Count(t => t.Status == TicketStatus.Closed && t.ClosedAt >= monthStartUtc)))
            .SingleOrDefaultAsync(cancellationToken);

        return summary ?? new RequesterTicketsSummaryDto(0, 0, 0);
    }

    public Task<RequesterTicketDetailsDto?> GetForRequesterAsync(int ticketId, int requesterId,
        CancellationToken cancellationToken) =>
        // The scope is part of the query (spec 6.2): another user's ticket is simply not found.
        db.Tickets
            .AsNoTracking()
            .Where(t => t.Id == ticketId && t.RequesterId == requesterId)
            .Select(t => new RequesterTicketDetailsDto(
                t.Id,
                t.Subject,
                t.Description,
                t.Status,
                t.Category != null ? t.Category.Name : null,
                t.AssignedToUser != null ? t.AssignedToUser.FirstName + " " + t.AssignedToUser.LastName : null,
                t.CreatedAt,
                t.UpdatedAt,
                t.ResolvedAt,
                t.ClosedAt,
                // Index TicketStatusHistory(TicketId, ChangedAt) – spec 8.4. The reason is deliberately not selected.
                t.StatusHistory
                    .OrderBy(h => h.ChangedAt).ThenBy(h => h.Id)
                    .Select(h => new RequesterTicketHistoryItemDto(
                        h.PreviousStatus,
                        h.NewStatus,
                        h.ChangedAt,
                        h.ChangedByUser != null ? h.ChangedByUser.FirstName + " " + h.ChangedByUser.LastName : null))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);

    // ===================== User administration (task 2.3) =====================

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
