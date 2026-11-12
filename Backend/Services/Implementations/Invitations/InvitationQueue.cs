using Common.Enums;
using Common.Models;
using Repositories.Interfaces;

namespace Services.Implementations.Invitations;

/// <summary>
/// Shared by user creation, resend, password reset and set-password, so "queue an invitation" and "cancel what is
/// open" mean the same everywhere. Invariant: a user has at most one valid link and at most one open job.
/// </summary>
internal static class InvitationQueue
{
    /// <summary>A queue row for the user (by navigation, so it also works for a user not saved yet).</summary>
    public static InvitationEmailJob NewJob(User user, DateTime now) => new()
    {
        OrganizationId = user.OrganizationId,
        UserId = user.Id,
        User = user,
        Status = InvitationJobStatus.Pending,
        NextAttemptAt = now,
        CreatedAt = now,
    };

    /// <summary>Open jobs → Cancelled, valid links → revoked. Writes immediately: call inside a transaction.</summary>
    public static async Task CancelOpenAsync(IUserInvitationRepository invitations, int userId, DateTime now,
        CancellationToken cancellationToken)
    {
        await invitations.CancelOpenJobsAsync(userId, now, cancellationToken);
        await invitations.RevokeOpenInvitationsAsync(userId, now, cancellationToken);
    }
}
