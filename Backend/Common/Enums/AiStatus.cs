namespace Common.Enums;

/// <summary>State of the AI analysis of a ticket (spec 3.1).</summary>
public enum AiStatus : byte
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4,

    /// <summary>
    /// No analysis was requested: the ticket was classified by the fallback right away (task 3.3, before the AI exists;
    /// from task 4.3 also when Ai:Enabled = false, spec 5.6). Not a failure – there is nothing to retry.
    /// </summary>
    Skipped = 5
}
