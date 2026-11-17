using Common.Constants;
using Common.DTOs.Dashboard;
using Common.Enums;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Repositories.Data;
using Repositories.Interfaces;
using static Repositories.Implementations.DashboardMetrics;

namespace Repositories.Implementations;

/// <summary>
/// The dashboard queries (task 5.1). Each method is one SQL statement: the metrics of <see cref="DashboardMetrics"/> are
/// inlined into a projection and aggregated by GROUP BY (spec 13) – COUNT(CASE …), AVG(CASE …).
/// Indexes of spec 8.4, by scope (DashboardScope):
/// <list type="bullet">
/// <item>view=requesters – IX (RequesterDepartmentId, CreatedAt) INCLUDE (Status, CategoryId).</item>
/// <item>view=handling – IX (AssignedDepartmentId, Status, DueAt).</item>
/// <item>organization – IX (OrganizationId, CreatedAt) INCLUDE (Status, CategoryId, RequesterDepartmentId), and the
/// filtered IX_Tickets_DueAt_Open for the overdue count.</item>
/// </list>
/// Known gaps (measured in task 6.2, not changed here): ClassificationSource, ResolvedAt and ClosedAt are in none of
/// these indexes (key lookups or a scan within the scope), and IX_Tickets_ResolvedAt_Resolved covers only Resolved.
/// </summary>
public sealed class DashboardRepository(AppDbContext db) : IDashboardRepository
{
    private IQueryable<Ticket> Tickets(DashboardScope scope) => db.Tickets.AsNoTracking().Where(InScope(scope));

    public async Task<DashboardSnapshot> GetSnapshotAsync(DashboardScope scope, DashboardWindow window,
        CancellationToken cancellationToken)
    {
        // Index: the scope's index; the Where narrows to the rows that can count (overdue ⊂ open).
        // One row: SELECT COUNT(CASE …) × 3 … GROUP BY a constant; no row at all without tickets.
        var newThisWeek = OpenedSince(window.WeekStartUtc);
        return await Tickets(scope)
                   .Where(Or(IsOpen, newThisWeek))
                   .Select(Row((Ticket t, bool open, bool overdue, bool isNew) =>
                           new SnapshotRow { Open = open, Overdue = overdue, New = isNew },
                       IsOpen, IsOverdue(window.NowUtc), newThisWeek))
                   .GroupBy(_ => 1)
                   .Select(g => new DashboardSnapshot(
                       g.Count(r => r.Open),
                       g.Count(r => r.Overdue),
                       g.Count(r => r.New)))
                   .SingleOrDefaultAsync(cancellationToken)
               ?? DashboardSnapshot.Empty;
    }

    public async Task<IReadOnlyList<DashboardOpenedGroup>> GetOpenedGroupsAsync(DashboardScope scope,
        DashboardWindow window, CancellationToken cancellationToken) =>
        // Index: the scope's index (seek on CreatedAt for requesters / organization); ClassificationSource → key lookup.
        // At most categories × 7 statuses × 2 rows.
        await Tickets(scope)
            .Where(OpenedIn(window))
            .Select(Row((Ticket t, bool ai) => new OpenedRow { CategoryId = t.CategoryId, Status = t.Status, Ai = ai },
                AutoClassified))
            .GroupBy(r => new { r.CategoryId, r.Status, r.Ai })
            .Select(g => new DashboardOpenedGroup(g.Key.CategoryId, g.Key.Status, g.Key.Ai, g.Count()))
            .ToListAsync(cancellationToken);

    public async Task<DashboardResolvedSummary> GetResolvedSummaryAsync(DashboardScope scope, DashboardWindow window,
        CancellationToken cancellationToken) =>
        // Index: the scope's index (ResolvedAt is not in it – a range scan within the scope).
        await Tickets(scope)
            .Where(ResolvedIn(window))
            .Select(Row((Ticket t, int? seconds, bool hasDueAt, bool met) =>
                    new ResolvedRow { Seconds = seconds, HasDueAt = hasDueAt, Met = met },
                ResolutionSeconds, HasDueAt, MetSla))
            .GroupBy(_ => 1)
            .Select(g => new DashboardResolvedSummary(
                g.Count(),
                g.Average(r => (double?)r.Seconds),
                g.Count(r => r.HasDueAt),
                g.Count(r => r.Met)))
            .SingleOrDefaultAsync(cancellationToken)
        ?? DashboardResolvedSummary.Empty;

    public async Task<IReadOnlyList<DashboardDayCount>> GetOpenedPerDayAsync(DashboardScope scope,
        DashboardWindow window, CancellationToken cancellationToken)
    {
        // Index: the scope's index (seek on CreatedAt). GROUP BY the day in Israel (AT TIME ZONE).
        var days = await Tickets(scope)
            .Where(OpenedIn(window))
            .GroupBy(OpenedDay)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        return days.Select(d => new DashboardDayCount(DateOnly.FromDateTime(d.Day), d.Count)).ToList();
    }

    public async Task<IReadOnlyList<DashboardDayCount>> GetClosedPerDayAsync(DashboardScope scope,
        DashboardWindow window, CancellationToken cancellationToken)
    {
        // No index of spec 8.4 has ClosedAt: a scan within the scope (measured in task 6.2).
        var days = await Tickets(scope)
            .Where(ClosedIn(window))
            .GroupBy(ClosedDay)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        return days.Select(d => new DashboardDayCount(DateOnly.FromDateTime(d.Day), d.Count)).ToList();
    }

    public async Task<IReadOnlyList<DashboardTicketDto>> GetOldestOpenAsync(DashboardScope scope,
        DashboardWindow window, int take, CancellationToken cancellationToken)
    {
        // Index: the scope's index + IX_Tickets_DueAt_Open; TOP(n) with ORDER BY CASE (overdue) DESC, CreatedAt, Id.
        const int excerpt = TicketFieldLimits.ExcerptLength;
        return await Tickets(scope)
            .Where(IsOpen)
            .OrderByDescending(IsOverdue(window.NowUtc))
            .ThenBy(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .Take(take)
            .Select(TicketSlaQueries.WithIsOverdue<DashboardTicketDto>((t, isOverdue) => new DashboardTicketDto(
                t.Id,
                t.Subject ?? (t.Description.Length > excerpt ? t.Description.Substring(0, excerpt) : t.Description),
                t.Subject == null,
                t.RequesterDepartment.Name,
                t.AssignedDepartment != null ? t.AssignedDepartment.Name : null,
                t.Status,
                t.CreatedAt,
                t.DueAt,
                isOverdue), window.NowUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DashboardDepartmentStats>> GetHandlingDepartmentStatsAsync(DashboardWindow window,
        CancellationToken cancellationToken)
    {
        // Index: IX (AssignedDepartmentId, Status, DueAt) for the open part; ResolvedAt is not in it (scan of the
        // organization's tickets that are open or resolved in the range).
        var resolvedIn = ResolvedIn(window);
        return await db.Tickets.AsNoTracking()
            .Where(Or(IsOpen, resolvedIn))
            .Select(Row((Ticket t, bool open, bool overdue, bool resolved, int? seconds) => new ComparisonRow
                {
                    DepartmentId = t.AssignedDepartmentId,
                    Open = open,
                    Overdue = overdue,
                    Resolved = resolved,
                    Seconds = resolved ? seconds : null,
                },
                IsOpen, IsOverdue(window.NowUtc), resolvedIn, ResolutionSeconds))
            .GroupBy(r => r.DepartmentId)
            .Select(g => new DashboardDepartmentStats(
                g.Key,
                g.Count(r => r.Open),
                g.Count(r => r.Overdue),
                g.Count(r => r.Resolved),
                g.Average(r => (double?)r.Seconds)))
            .ToListAsync(cancellationToken);
    }

    // Intermediate projections: classes with setters (not positional records), so EF can read their members back in
    // the GROUP BY that follows.

    private sealed class SnapshotRow
    {
        public bool Open { get; init; }
        public bool Overdue { get; init; }
        public bool New { get; init; }
    }

    private sealed class OpenedRow
    {
        public int? CategoryId { get; init; }
        public TicketStatus Status { get; init; }
        public bool Ai { get; init; }
    }

    private sealed class ResolvedRow
    {
        public int? Seconds { get; init; }
        public bool HasDueAt { get; init; }
        public bool Met { get; init; }
    }

    private sealed class ComparisonRow
    {
        public int? DepartmentId { get; init; }
        public bool Open { get; init; }
        public bool Overdue { get; init; }
        public bool Resolved { get; init; }
        public int? Seconds { get; init; }
    }
}
