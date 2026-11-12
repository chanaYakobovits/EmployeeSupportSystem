using Common.Enums;

namespace Common.DTOs.Invitations;

/// <summary>
/// The invitation state of a user, for the admin. <see cref="At"/>: when it reached this state
/// (queued / sent / failed / used / expired / revoked).
/// </summary>
public sealed record InvitationStatusDto(InvitationStatus Status, DateTime At);

/// <summary>A queue row claimed by the background sender: which job, in which organization.</summary>
public sealed record ClaimedInvitationJob(int JobId, int OrganizationId);
