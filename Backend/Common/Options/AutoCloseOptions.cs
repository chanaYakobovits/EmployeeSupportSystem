namespace Common.Options;

/// <summary>
/// The background job that closes resolved tickets the employee did not confirm (spec 3.3, 10.2, 14.9). Bound from the
/// "AutoClose" configuration section. The 7 days themselves are a business rule (TicketStatusRules.ResolvedWindow,
/// shared with the reopen window), not a setting.
/// </summary>
public sealed class AutoCloseOptions
{
    public const string SectionName = "AutoClose";

    public bool Enabled { get; set; } = true;

    public int IntervalMinutes { get; set; } = 60;

    /// <summary>Tickets closed per round, at most (the rest wait for the next round).</summary>
    public int BatchSize { get; set; } = 200;
}
