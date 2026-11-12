using Common.Enums;

namespace Common.Models;

/// <summary>
/// Queue row "send an invitation to user X" (same pattern as <see cref="AiAnalysisJob"/>, spec 5.2).
/// Written in the same transaction as the user. Holds no token, no e-mail address and no content: the token is created
/// only when the e-mail is sent, and the address is read from Users at that moment.
/// </summary>
public class InvitationEmailJob
{
    public int Id { get; set; }

    /// <summary>The worker binds its scope to this organization before touching filtered tables.</summary>
    public int OrganizationId { get; set; }

    public int UserId { get; set; }
    public InvitationJobStatus Status { get; set; }
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime? LockedUntil { get; set; }

    /// <summary>Error code only (<see cref="EmailErrorCode"/> or a cancellation reason), never content.</summary>
    public string? LastErrorCode { get; set; }

    /// <summary>The invitation (token hash) created by the latest send attempt.</summary>
    public int? InvitationId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public User User { get; set; } = null!;
    public UserInvitation? Invitation { get; set; }
}
