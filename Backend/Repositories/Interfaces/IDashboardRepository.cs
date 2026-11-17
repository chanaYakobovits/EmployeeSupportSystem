using Common.DTOs.Dashboard;

namespace Repositories.Interfaces;

/// <summary>
/// The dashboard queries (task 5.1, spec 7.9): every method is one grouped SQL query over the tickets of the signed-in
/// user's organization (the query filter applies) and returns aggregated rows only – never tickets. The metrics are
/// defined in DashboardMetrics. Uses DATEDIFF and AT TIME ZONE, so it needs SQL Server (not InMemory).
/// The methods run one after the other on the request's context.
/// </summary>
public interface IDashboardRepository
{
    /// <summary>Open, overdue open and new this week – the current state.</summary>
    Task<DashboardSnapshot> GetSnapshotAsync(DashboardScope scope, DashboardWindow window,
        CancellationToken cancellationToken);

    /// <summary>Opened in the range, grouped by category, current status and AI classification.</summary>
    Task<IReadOnlyList<DashboardOpenedGroup>> GetOpenedGroupsAsync(DashboardScope scope, DashboardWindow window,
        CancellationToken cancellationToken);

    /// <summary>Resolved in the range: count, average resolution time, SLA compliance.</summary>
    Task<DashboardResolvedSummary> GetResolvedSummaryAsync(DashboardScope scope, DashboardWindow window,
        CancellationToken cancellationToken);

    /// <summary>Opened per day in Israel, in the range. Days without tickets are missing.</summary>
    Task<IReadOnlyList<DashboardDayCount>> GetOpenedPerDayAsync(DashboardScope scope, DashboardWindow window,
        CancellationToken cancellationToken);

    /// <summary>Closed per day in Israel, in the range. Days without tickets are missing.</summary>
    Task<IReadOnlyList<DashboardDayCount>> GetClosedPerDayAsync(DashboardScope scope, DashboardWindow window,
        CancellationToken cancellationToken);

    /// <summary>The open tickets: overdue first, then the oldest; at most <paramref name="take"/>.</summary>
    Task<IReadOnlyList<DashboardTicketDto>> GetOldestOpenAsync(DashboardScope scope, DashboardWindow window, int take,
        CancellationToken cancellationToken);

    /// <summary>
    /// The organization by handling department (null = not routed): open and overdue now, resolved in the range and
    /// their average resolution time. Only departments with such tickets are returned.
    /// </summary>
    Task<IReadOnlyList<DashboardDepartmentStats>> GetHandlingDepartmentStatsAsync(DashboardWindow window,
        CancellationToken cancellationToken);
}
