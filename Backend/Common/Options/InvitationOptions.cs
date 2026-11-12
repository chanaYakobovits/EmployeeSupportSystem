namespace Common.Options;

/// <summary>Invitation e-mails and their background sender. Bound from the "Invitations" configuration section.</summary>
public sealed class InvitationOptions
{
    public const string SectionName = "Invitations";

    /// <summary>Base address of the Angular app; the link is <c>{FrontendBaseUrl}/set-password#token=...</c>.</summary>
    public string FrontendBaseUrl { get; set; } = string.Empty;

    /// <summary>Validity of a link, counted from the moment it is sent.</summary>
    public int ExpiryHours { get; set; } = 72;

    public int WorkerIntervalSeconds { get; set; } = 10;

    public int BatchSize { get; set; } = 10;

    /// <summary>Send attempts per job, including the first one.</summary>
    public int MaxAttempts { get; set; } = 4;

    /// <summary>Delay before attempt 2, 3, ... (the last value repeats if there are more attempts than values).</summary>
    public int[] RetryDelaysMinutes { get; set; } = [1, 5, 30];

    /// <summary>
    /// How long a claimed job stays locked. A job still "Processing" after this (the server stopped mid-send) is
    /// claimed again.
    /// </summary>
    public int LockMinutes { get; set; } = 5;
}
