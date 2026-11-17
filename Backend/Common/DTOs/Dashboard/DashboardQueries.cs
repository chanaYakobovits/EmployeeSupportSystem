using Common.Enums;

namespace Common.DTOs.Dashboard;

/// <summary>
/// The date range of a dashboard (spec 7.9): calendar days in Israel, both included. Without both – the last 30 days
/// up to today; with one – 30 days from it / up to it. At most <see cref="DashboardLimits.MaxRangeDays"/> days.
/// </summary>
public class DashboardRangeQuery
{
    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }
}

/// <summary>GET /api/dashboard/departments/{id} (spec 9.3). View missing → requesters.</summary>
public sealed class DepartmentDashboardQuery : DashboardRangeQuery
{
    public DashboardView? View { get; set; }
}

/// <summary>GET /api/dashboard/organization (spec 9.3).</summary>
public sealed class OrganizationDashboardQuery : DashboardRangeQuery;

public static class DashboardLimits
{
    /// <summary>The default range (spec 7.9): 30 days, today included.</summary>
    public const int DefaultRangeDays = 30;

    /// <summary>A year, a leap year included.</summary>
    public const int MaxRangeDays = 366;

    /// <summary>The table of the oldest / overdue open tickets (spec 7.9).</summary>
    public const int OldestOpenCount = 10;
}
