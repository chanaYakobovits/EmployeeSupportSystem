using Common.DTOs.Categories;
using Common.DTOs.Dashboard;
using Common.DTOs.Departments;
using Common.Enums;

namespace Services.Implementations.Dashboard;

/// <summary>
/// Manual mapping of the aggregated rows to the dashboard DTOs (spec 13). Pure: only the few grouped rows the database
/// returned – percentages, hours, the missing days of the trend and the names.
/// </summary>
internal static class DashboardMapping
{
    /// <summary>The comparison's row of tickets without a handling department.</summary>
    public const string NotRoutedName = "לא נותבו";

    public static DashboardCardsDto Cards(DashboardSnapshot snapshot, IReadOnlyList<DashboardOpenedGroup> opened,
        DashboardResolvedSummary resolved)
    {
        var openedCount = opened.Sum(g => g.Count);
        var autoClassified = opened.Where(g => g.AutoClassified).Sum(g => g.Count);
        return new DashboardCardsDto(
            snapshot.Open,
            snapshot.NewThisWeek,
            snapshot.OverdueOpen,
            openedCount,
            resolved.Resolved,
            Hours(resolved.AverageSeconds),
            Percent(resolved.MetSla, resolved.WithDueAt),
            Percent(autoClassified, openedCount));
    }

    /// <summary>Categories with tickets, the most tickets first (then by name); not classified yet – last.</summary>
    public static IReadOnlyList<DashboardCategoryCountDto> ByCategory(IReadOnlyList<DashboardOpenedGroup> opened,
        IReadOnlyList<CategoryDto> categories)
    {
        var names = categories.ToDictionary(c => c.Id, c => c.Name);
        return opened
            .GroupBy(g => g.CategoryId)
            .Select(g => new DashboardCategoryCountDto(
                g.Key,
                g.Key is { } id ? names.GetValueOrDefault(id) : null,
                g.Sum(x => x.Count)))
            .OrderBy(c => c.CategoryId is null)
            .ThenByDescending(c => c.Count)
            .ThenBy(c => c.CategoryName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Every status in the enum's order, zeros included (a stable chart).</summary>
    public static IReadOnlyList<DashboardStatusCountDto> ByStatus(IReadOnlyList<DashboardOpenedGroup> opened)
    {
        var counts = opened.GroupBy(g => g.Status).ToDictionary(g => g.Key, g => g.Sum(x => x.Count));
        return Enum.GetValues<TicketStatus>()
            .Select(status => new DashboardStatusCountDto(status, counts.GetValueOrDefault(status)))
            .ToList();
    }

    /// <summary>Every day of the range, days without tickets as 0.</summary>
    public static IReadOnlyList<DashboardTrendPointDto> Trend(DashboardRangeDto range,
        IReadOnlyList<DashboardDayCount> opened, IReadOnlyList<DashboardDayCount> closed)
    {
        var openedByDay = opened.ToDictionary(d => d.Day, d => d.Count);
        var closedByDay = closed.ToDictionary(d => d.Day, d => d.Count);
        var days = range.To.DayNumber - range.From.DayNumber + 1;
        return Enumerable.Range(0, days)
            .Select(range.From.AddDays)
            .Select(day => new DashboardTrendPointDto(day, openedByDay.GetValueOrDefault(day), closedByDay.GetValueOrDefault(day)))
            .ToList();
    }

    /// <summary>
    /// Every active department (zeros included), inactive ones only with tickets, by name; "not routed" last and only
    /// with tickets – so the open and overdue columns add up to the cards.
    /// </summary>
    public static IReadOnlyList<DashboardDepartmentComparisonDto> Comparison(IReadOnlyList<DashboardDepartmentStats> stats,
        IReadOnlyList<DepartmentDto> departments)
    {
        var byDepartment = stats.Where(s => s.DepartmentId is not null).ToDictionary(s => s.DepartmentId!.Value);
        var rows = departments
            .Where(d => d.IsActive || byDepartment.ContainsKey(d.Id))
            .OrderBy(d => d.Name, StringComparer.Ordinal)
            .Select(d => Row(d.Id, d.Name, d.IsActive, byDepartment.GetValueOrDefault(d.Id)))
            .ToList();

        if (stats.FirstOrDefault(s => s.DepartmentId is null) is { } notRouted)
        {
            rows.Add(Row(null, NotRoutedName, true, notRouted));
        }

        return rows;
    }

    private static DashboardDepartmentComparisonDto Row(int? id, string name, bool isActive, DashboardDepartmentStats? s) =>
        new(id, name, isActive, s?.Open ?? 0, s?.OverdueOpen ?? 0, s?.ResolvedInRange ?? 0,
            Hours(s?.AverageResolutionSeconds));

    /// <summary>0–100 with one decimal; null when there is nothing to divide by (not 0%).</summary>
    public static double? Percent(int part, int total) =>
        total == 0 ? null : Math.Round(100.0 * part / total, 1, MidpointRounding.AwayFromZero);

    public static double? Hours(double? seconds) =>
        seconds is { } s ? Math.Round(s / 3600, 1, MidpointRounding.AwayFromZero) : null;
}
