using Common.Enums;

namespace Common.DTOs.Dashboard;

// What the dashboard repository returns: already aggregated by the database (a handful of rows), never tickets.

/// <summary>Which tickets a dashboard counts. DepartmentId null = the whole organization (View is ignored).</summary>
public sealed record DashboardScope(int? DepartmentId, DashboardView View)
{
    public static readonly DashboardScope Organization = new(null, DashboardView.Requesters);
}

/// <summary>The instants the metrics are computed against (UTC). ToUtc is exclusive: the day after the range, at midnight.</summary>
/// <param name="WeekStartUtc">Midnight in Israel, 6 days before today: "new this week" = the last 7 days.</param>
public sealed record DashboardWindow(DateTime NowUtc, DateTime WeekStartUtc, DateTime FromUtc, DateTime ToUtc);

/// <summary>The current state: open, overdue, new this week.</summary>
public sealed record DashboardSnapshot(int Open, int OverdueOpen, int NewThisWeek)
{
    public static readonly DashboardSnapshot Empty = new(0, 0, 0);
}

/// <summary>Opened in the range, grouped by category, current status and whether the AI classified them.</summary>
public sealed record DashboardOpenedGroup(int? CategoryId, TicketStatus Status, bool AutoClassified, int Count);

/// <summary>Resolved in the range.</summary>
/// <param name="AverageSeconds">Opening → resolution; null when <paramref name="Resolved"/> is 0.</param>
/// <param name="WithDueAt">The base of the SLA compliance (a resolved ticket always has a due time; kept for safety).</param>
public sealed record DashboardResolvedSummary(int Resolved, double? AverageSeconds, int WithDueAt, int MetSla)
{
    public static readonly DashboardResolvedSummary Empty = new(0, null, 0, 0);
}

/// <summary>A day in Israel with its count. Days without tickets are not returned.</summary>
public sealed record DashboardDayCount(DateOnly Day, int Count);

/// <summary>The comparison by handling department (null = not routed).</summary>
public sealed record DashboardDepartmentStats(
    int? DepartmentId,
    int Open,
    int OverdueOpen,
    int ResolvedInRange,
    double? AverageResolutionSeconds);
