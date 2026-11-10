namespace Common.Exceptions;

/// <summary>
/// Too many failed logins for this identifier (→ 401, type "account-locked", spec 7.1).
/// Thrown for unknown identifiers too, with the same message, so it reveals nothing about existence.
/// </summary>
public sealed class AccountLockedException : AuthenticationFailedException
{
    public const string LockedTitle = "החשבון ננעל זמנית עקב ניסיונות התחברות כושלים";

    public AccountLockedException(TimeSpan retryAfter)
        : base(LockedTitle, BuildDetail(retryAfter), "account-locked")
    {
        RetryAfter = retryAfter;
    }

    public TimeSpan RetryAfter { get; }

    /// <summary>Whole minutes, rounded up, at least 1.</summary>
    public int RetryAfterMinutes => Math.Max(1, (int)Math.Ceiling(RetryAfter.TotalMinutes));

    private static string BuildDetail(TimeSpan retryAfter)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalMinutes));
        return minutes == 1
            ? "ניתן לנסות שוב בעוד דקה."
            : $"ניתן לנסות שוב בעוד {minutes} דקות.";
    }
}
