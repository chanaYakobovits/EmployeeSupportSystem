using System.Linq.Expressions;
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

    public Task<TicketAccessFacts?> GetAccessFactsAsync(int ticketId, CancellationToken cancellationToken) =>
        db.Tickets
            .AsNoTracking()
            .Where(t => t.Id == ticketId)
            .Select(t => new TicketAccessFacts(t.Id, t.RequesterId, t.RequesterDepartmentId, t.AssignedDepartmentId,
                t.AssignedToUserId, t.Status))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Ticket?> FindForUpdateAsync(int ticketId, CancellationToken cancellationToken) =>
        db.Tickets.SingleOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

    public void ExpectRowVersion(Ticket ticket, byte[] rowVersion) =>
        // The UPDATE is conditioned on this value: a version changed in the meantime → DbUpdateConcurrencyException.
        db.Entry(ticket).Property(t => t.RowVersion).OriginalValue = rowVersion;

    public Task<RequesterTicketDetailsDto?> GetDetailsAsync(int ticketId, CancellationToken cancellationToken) =>
        // The viewing scope was checked by TicketAccess before this query.
        db.Tickets
            .AsNoTracking()
            .Where(t => t.Id == ticketId)
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

    // ===================== The staff lists and view (task 3.4) =====================

    public Task<PagedResult<StaffTicketListItemDto>> GetStaffPageAsync(Expression<Func<Ticket, bool>> scope,
        StaffTicketsQuery query, DateTime? fromUtc, DateTime? toUtc, DateTime now, CancellationToken cancellationToken)
    {
        // The scope comes first: it is what the indexes of spec 8.4 serve – (AssignedToUserId, Status) for "assigned",
        // (AssignedDepartmentId, Status, DueAt) for the queue and the handling department, (RequesterDepartmentId,
        // CreatedAt) / (OrganizationId, CreatedAt) for the department scope. Every other filter narrows it down.
        var tickets = db.Tickets.AsNoTracking().Where(scope);

        if (query.Status is { Count: > 0 } statuses)
        {
            tickets = tickets.Where(t => statuses.Contains(t.Status));
        }

        if (query.CategoryId is { } categoryId)
        {
            tickets = tickets.Where(t => t.CategoryId == categoryId);
        }

        if (query.Urgency is { Count: > 0 } urgencyFilter)
        {
            var notSet = urgencyFilter.Contains(UrgencyFilter.NotSet);
            var urgencies = urgencyFilter
                .Where(u => u != UrgencyFilter.NotSet)
                .Select(u => (Urgency?)(Urgency)u)
                .Distinct()
                .ToList();
            tickets = (notSet, urgencies.Count > 0) switch
            {
                (true, true) => tickets.Where(t => t.Urgency == null || urgencies.Contains(t.Urgency)),
                (true, false) => tickets.Where(t => t.Urgency == null),
                _ => tickets.Where(t => urgencies.Contains(t.Urgency)),
            };
        }

        if (fromUtc is { } from)
        {
            tickets = tickets.Where(t => t.CreatedAt >= from);
        }

        if (toUtc is { } to)
        {
            tickets = tickets.Where(t => t.CreatedAt < to);
        }

        if (query.OverdueOnly)
        {
            tickets = tickets.Where(TicketSlaQueries.IsOverdue(now));
        }

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            // LIKE on the subject and the requester's name (spec 8.4), within the scope and the filters above.
            tickets = tickets.Where(t =>
                (t.Subject != null && t.Subject.Contains(search))
                || t.Requester.FirstName.Contains(search)
                || t.Requester.LastName.Contains(search)
                || (t.Requester.FirstName + " " + t.Requester.LastName).Contains(search));
        }

        var sortBy = query.SortBy ?? StaffTicketSortFields.Default;
        var desc = query.SortDir is null ? StaffTicketSortFields.IsDescendingByDefault(sortBy) : query.IsDescending;
        var ordered = sortBy switch
        {
            // No urgency sorts as Medium, like its SLA (spec 3.4). Ties: the nearest due time, then the oldest ticket.
            StaffTicketSortFields.Urgency => tickets
                .OrderByDirection(t => t.Urgency ?? Urgency.Medium, desc)
                .ThenBy(t => t.DueAt == null).ThenBy(t => t.DueAt)
                .ThenBy(t => t.Id),
            StaffTicketSortFields.CreatedAt => tickets
                .OrderByDirection(t => t.CreatedAt, desc)
                .ThenByDirection(t => t.Id, desc),
            StaffTicketSortFields.UpdatedAt => tickets
                .OrderByDirection(t => t.UpdatedAt, desc)
                .ThenByDirection(t => t.Id, desc),
            StaffTicketSortFields.Status => tickets
                .OrderByDirection(t => t.Status, desc)
                .ThenBy(t => t.Id),
            // dueAt (spec 4.4: by due time and urgency). A ticket without a due time (not routed yet) is last either way.
            _ => tickets
                .OrderBy(t => t.DueAt == null).ThenByDirection(t => t.DueAt, desc)
                .ThenByDescending(t => t.Urgency ?? Urgency.Medium)
                .ThenBy(t => t.Id),
        };

        const int excerpt = TicketFieldLimits.ExcerptLength;
        var projection = TicketSlaQueries.WithIsOverdue<StaffTicketListItemDto>((t, isOverdue) =>
            new StaffTicketListItemDto(
                t.Id,
                t.Subject ?? (t.Description.Length > excerpt ? t.Description.Substring(0, excerpt) : t.Description),
                t.Subject == null,
                t.Requester.FirstName + " " + t.Requester.LastName,
                t.RequesterDepartment.Name,
                t.Category != null ? t.Category.Name : null,
                t.Urgency,
                t.Status,
                t.AssignedToUser != null ? t.AssignedToUser.FirstName + " " + t.AssignedToUser.LastName : null,
                t.AssignedDepartment != null ? t.AssignedDepartment.Name : null,
                t.DueAt,
                isOverdue,
                t.ClassificationSource,
                t.CreatedAt,
                t.UpdatedAt,
                t.RowVersion), now);

        return tickets.ToPagedResultAsync(ordered, projection, query, cancellationToken);
    }

    public Task<StaffTicketDetailsDto?> GetStaffDetailsAsync(int ticketId, StaffTicketPermissionsDto permissions,
        DateTime now, CancellationToken cancellationToken)
    {
        // Plain values: only they are captured by the projection.
        var canReply = permissions.CanReply;
        var canWriteInternal = permissions.CanWriteInternal;
        var canReclassify = permissions.CanReclassify;
        var canAssign = permissions.CanAssign;
        var canTransfer = permissions.CanTransferToDepartment;

        // The viewing scope was checked by TicketAccess before this query.
        return db.Tickets
            .AsNoTracking()
            .Where(t => t.Id == ticketId)
            .Select(TicketSlaQueries.WithIsOverdue<StaffTicketDetailsDto?>((t, isOverdue) => new StaffTicketDetailsDto(
                t.Id,
                t.Subject,
                t.Description,
                t.Status,
                new StaffTicketRequesterDto(
                    t.RequesterId,
                    t.Requester.FirstName + " " + t.Requester.LastName,
                    t.RequesterDepartment.Name,
                    t.Requester.JobTitle),
                t.Category != null ? new NamedRefDto(t.Category.Id, t.Category.Name) : null,
                t.Urgency,
                t.ClassificationSource,
                t.DueAt,
                isOverdue,
                t.AssignedToUser != null
                    ? new NamedRefDto(t.AssignedToUser.Id, t.AssignedToUser.FirstName + " " + t.AssignedToUser.LastName)
                    : null,
                t.AssignedDepartment != null ? new NamedRefDto(t.AssignedDepartment.Id, t.AssignedDepartment.Name) : null,
                t.CreatedAt,
                t.UpdatedAt,
                t.ResolvedAt,
                t.ClosedAt,
                t.RowVersion,
                new StaffTicketPermissionsDto(canReply, canWriteInternal, canReclassify, canAssign, canTransfer)), now))
            .SingleOrDefaultAsync(cancellationToken);
    }

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
