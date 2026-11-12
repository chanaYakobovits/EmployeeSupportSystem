namespace Common.Enums;

/// <summary>Invitation state shown to the admin (users list / details). Derived, not stored.</summary>
public enum InvitationStatus
{
    /// <summary>Queued, not sent yet (or a retry is scheduled).</summary>
    Pending,

    /// <summary>Sent; the link is still valid.</summary>
    Sent,

    /// <summary>The employee set a password with the link.</summary>
    Used,

    /// <summary>Sent, but the link expired unused.</summary>
    Expired,

    /// <summary>Sending failed permanently; the admin can resend.</summary>
    Failed,

    /// <summary>The link was cancelled by a password reset by the admin.</summary>
    Revoked,
}
