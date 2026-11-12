namespace Services.Implementations;

/// <summary>
/// Israel time (spec 11.6): calendar boundaries such as "this month" follow the user's clock, while everything is
/// stored and compared in UTC.
/// </summary>
internal static class IsraelTime
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");

    public static DateTime FromUtc(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    /// <summary>Midnight of the 1st of the current Israeli month, as UTC.</summary>
    public static DateTime StartOfMonthUtc(DateTime utcNow)
    {
        var local = FromUtc(utcNow);
        var startLocal = new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(startLocal, Zone);
    }
}
