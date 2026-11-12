using Common.DTOs.Invitations;

namespace Services.Interfaces;

/// <summary>
/// The work of the invitation e-mail worker (API/BackgroundJobs). Runs without a signed-in user.
/// </summary>
public interface IInvitationEmailProcessor
{
    /// <summary>
    /// Claims the due jobs of all organizations (atomic, safe with several servers). Call in a scope without an
    /// organization; then process each job in a scope bound to its organization.
    /// </summary>
    Task<IReadOnlyList<ClaimedInvitationJob>> ClaimDueAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates the token, stores only its hash and sends the e-mail; schedules a retry or marks the job Failed.
    /// Must run in a scope bound to the job's organization (ISystemOrganizationContext.Enter).
    /// </summary>
    Task ProcessAsync(int jobId, CancellationToken cancellationToken);
}
