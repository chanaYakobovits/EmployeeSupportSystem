namespace Common.Enums;

/// <summary>State of an <see cref="Models.InvitationEmailJob"/> (same values as <see cref="AiJobStatus"/> + Cancelled).</summary>
public enum InvitationJobStatus : byte
{
    Pending = 1,
    Processing = 2,
    Succeeded = 3,
    Failed = 4,

    /// <summary>Replaced by a resend, a password reset or a password set; or the user can no longer be invited.</summary>
    Cancelled = 5,
}
