namespace Common.Constants;

/// <summary>"נתח מחדש" (task 4.4).</summary>
public static class AiRetryLimits
{
    /// <summary>
    /// The minimal time between two analysis jobs of one ticket (any job: the opening's too). Per ticket, not per user:
    /// two handlers of the same queue share it. Counted from the database, so only a request that queued a job counts.
    /// </summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);
}
