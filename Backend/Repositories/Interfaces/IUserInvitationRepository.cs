using Common.DTOs.Invitations;
using Common.Models;

namespace Repositories.Interfaces;

/// <summary>
/// Invitation tokens (<see cref="UserInvitation"/>) and the send queue (<see cref="InvitationEmailJob"/>).
/// The bulk methods (Cancel / Revoke / TryMarkUsed) write immediately; call them inside
/// <see cref="IUnitOfWork.ExecuteInTransactionAsync{T}"/> together with the staged changes they belong to.
/// </summary>
public interface IUserInvitationRepository
{
    /// <summary>Stages a queue row; written by <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
    void AddJob(InvitationEmailJob job);

    /// <summary>Stages an invitation; written by <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
    void AddInvitation(UserInvitation invitation);

    /// <summary>Pending / Processing jobs of the user → Cancelled. Returns the number of jobs cancelled.</summary>
    Task<int> CancelOpenJobsAsync(int userId, DateTime now, CancellationToken cancellationToken);

    /// <summary>Still valid invitations (not used, not revoked, not expired) of the user → RevokedAt = now.</summary>
    Task<int> RevokeOpenInvitationsAsync(int userId, DateTime now, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically claims up to <paramref name="batchSize"/> due jobs of all organizations (Pending and due, or
    /// Processing with an expired lock): Status → Processing, LockedUntil, Attempts + 1. Rows locked by another
    /// server are skipped (READPAST), so a job is never claimed twice. Spec 5.2.
    /// </summary>
    Task<IReadOnlyList<ClaimedInvitationJob>> ClaimDueJobsAsync(int batchSize, DateTime now, DateTime lockedUntil,
        CancellationToken cancellationToken);

    /// <summary>Tracked job with its current invitation, or null.</summary>
    Task<InvitationEmailJob?> FindJobAsync(int jobId, CancellationToken cancellationToken);

    /// <summary>
    /// Tracked invitation with its User (and Roles), or null. Works without a signed-in user (set-password is anonymous):
    /// the 256-bit token itself is the proof.
    /// </summary>
    Task<UserInvitation?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// UsedAt = now only if the invitation is still valid at this moment (one conditional UPDATE).
    /// Returns false if another request used it first, or it was revoked / expired meanwhile.
    /// </summary>
    Task<bool> TryMarkUsedAsync(int invitationId, DateTime now, CancellationToken cancellationToken);
}
