using Common.DTOs.Dashboard;

namespace Services.Implementations.Dashboard;

/// <summary>The days a dashboard covers (spec 7.9). Pure; the query is already validated.</summary>
internal static class DashboardRange
{
    /// <summary>
    /// Both days given → as is. Neither → the last 30 days, today included. Only one → 30 days from it / up to it.
    /// </summary>
    public static DashboardRangeDto Resolve(DashboardRangeQuery query, DateOnly today)
    {
        const int span = DashboardLimits.DefaultRangeDays - 1;
        return (query.From, query.To) switch
        {
            ({ } from, { } to) => new DashboardRangeDto(from, to),
            ({ } from, null) => new DashboardRangeDto(from, from.AddDays(span)),
            (null, { } to) => new DashboardRangeDto(to.AddDays(-span), to),
            _ => new DashboardRangeDto(today.AddDays(-span), today),
        };
    }

    /// <summary>The UTC instants of the range and of "this week" (spec 11.6: the days are Israel's).</summary>
    public static DashboardWindow Window(DashboardRangeDto range, DateOnly today, DateTime nowUtc) => new(
        nowUtc,
        IsraelTime.StartOfDayUtc(today.AddDays(-6)),
        IsraelTime.StartOfDayUtc(range.From),
        IsraelTime.StartOfDayUtc(range.To.AddDays(1)));

    public static DateOnly Today(DateTime nowUtc) => DateOnly.FromDateTime(IsraelTime.FromUtc(nowUtc));
}
