using Common.Constants;
using Common.DTOs.Invitations;
using Common.Enums;
using Common.Exceptions;
using Common.Interfaces;
using Common.Models;
using Common.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repositories.Interfaces;
using Services.Interfaces;

namespace Services.Implementations.Invitations;

/// <summary>
/// Sends one queued invitation (the queue holds only "invite user X"). The token is created only now, right before
/// sending: its hash is saved first, then the e-mail goes out. The raw token lives only in a local variable and in the
/// message – never in the queue, the database, the audit log or the log.
/// Every attempt creates a fresh token and revokes the previous attempt's one, so a user has at most one valid link.
/// Known limit: if the process stops between sending and marking the job done, the lock expires and the e-mail is
/// sent again with a new token (the first link is then revoked).
/// </summary>
public sealed class InvitationEmailProcessor(
    IUserInvitationRepository invitations,
    IUserRepository users,
    IAuditLogRepository auditLogs,
    IUnitOfWork unitOfWork,
    IEmailSender emailSender,
    InvitationEmailComposer composer,
    IDateTimeProvider clock,
    IOptions<InvitationOptions> options,
    ILogger<InvitationEmailProcessor> logger) : IInvitationEmailProcessor
{
    private InvitationOptions Options => options.Value;

    public Task<IReadOnlyList<ClaimedInvitationJob>> ClaimDueAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        return invitations.ClaimDueJobsAsync(Options.BatchSize, now, now.AddMinutes(Options.LockMinutes), cancellationToken);
    }

    public async Task ProcessAsync(int jobId, CancellationToken cancellationToken)
    {
        var job = await invitations.FindJobAsync(jobId, cancellationToken);
        if (job is not { Status: InvitationJobStatus.Processing })
        {
            // Cancelled by a resend / reset after it was claimed, or already handled.
            logger.LogInformation("Invitation job {JobId} skipped: no longer processing", jobId);
            return;
        }

        var now = clock.UtcNow;

        // Every claim counts an attempt. More than allowed means earlier attempts crashed without recording a result
        // (process stopped, unexpected exception): give up instead of claiming it forever.
        if (job.Attempts > Options.MaxAttempts)
        {
            if (job.Invitation is { UsedAt: null, RevokedAt: null } abandoned)
            {
                abandoned.RevokedAt = now;
            }

            Complete(job, InvitationJobStatus.Failed, nameof(EmailErrorCode.Unknown), now);
            auditLogs.Add(AuditEntries.ForSystem(AuditActions.InvitationSendFailed, job.OrganizationId, job.UserId, now));
            await SaveIgnoringConflictAsync(job.Id, cancellationToken);
            logger.LogWarning("Invitation job {JobId} for user {UserId} abandoned after {Attempt} attempts",
                job.Id, job.UserId, job.Attempts);
            return;
        }

        var user = await users.FindForUpdateAsync(job.UserId, cancellationToken);
        var cancelReason = user is null ? InvitationCancelReasons.UserNotFound
            : !user.IsActive ? InvitationCancelReasons.UserInactive
            : !user.MustChangePassword ? InvitationCancelReasons.PasswordAlreadySet
            : null;
        if (cancelReason is not null)
        {
            Complete(job, InvitationJobStatus.Cancelled, cancelReason, now);
            await SaveIgnoringConflictAsync(job.Id, cancellationToken);
            logger.LogInformation("Invitation job {JobId} for user {UserId} cancelled: {Reason}", job.Id, job.UserId, cancelReason);
            return;
        }

        var token = SecureTokenGenerator.NewValue();
        var invitation = new UserInvitation
        {
            UserId = user!.Id,
            TokenHash = SecureTokenGenerator.Hash(token),
            CreatedAt = now,
            ExpiresAt = now.AddHours(Options.ExpiryHours),
        };

        if (!await SaveNewInvitationAsync(job, invitation, now, cancellationToken))
        {
            return;
        }

        try
        {
            await emailSender.SendAsync(composer.Compose(user, token, invitation.ExpiresAt), cancellationToken);
        }
        catch (EmailSendException exception)
        {
            await HandleFailureAsync(job, invitation, exception, cancellationToken);
            return;
        }

        Complete(job, InvitationJobStatus.Succeeded, errorCode: null, clock.UtcNow);
        auditLogs.Add(AuditEntries.ForSystem(AuditActions.InvitationSent, job.OrganizationId, job.UserId, clock.UtcNow));
        await SaveIgnoringConflictAsync(job.Id, cancellationToken);
        logger.LogInformation("Invitation job {JobId}: e-mail sent to user {UserId} (attempt {Attempt})",
            job.Id, job.UserId, job.Attempts);
    }

    /// <summary>
    /// Revokes the previous attempt's link and stores the new hash, in one transaction. False if the job was cancelled
    /// meanwhile (its Status no longer matches): then nothing is saved and nothing is sent.
    /// </summary>
    private async Task<bool> SaveNewInvitationAsync(InvitationEmailJob job, UserInvitation invitation, DateTime now,
        CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await invitations.RevokeOpenInvitationsAsync(job.UserId, now, ct);
                invitations.AddInvitation(invitation);
                job.Invitation = invitation;
                await unitOfWork.SaveChangesAsync(ct);
                return true;
            }, cancellationToken);
            return true;
        }
        catch (ConflictException)
        {
            logger.LogInformation("Invitation job {JobId} cancelled while processing; nothing sent", job.Id);
            return false;
        }
    }

    private async Task HandleFailureAsync(InvitationEmailJob job, UserInvitation invitation, EmailSendException exception,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        // The link never reached anyone.
        invitation.RevokedAt = now;
        var code = exception.Code.ToString();

        if (exception.IsTransient && job.Attempts < Options.MaxAttempts)
        {
            job.Status = InvitationJobStatus.Pending;
            job.NextAttemptAt = now.AddMinutes(RetryDelayMinutes(job.Attempts));
            job.LockedUntil = null;
            job.LastErrorCode = code;
            await SaveIgnoringConflictAsync(job.Id, cancellationToken);
            logger.LogWarning(
                "Invitation job {JobId}: sending failed with {ErrorCode} ({ExceptionType}), attempt {Attempt} of {MaxAttempts}; retry at {NextAttemptAt}",
                job.Id, code, exception.ExceptionType, job.Attempts, Options.MaxAttempts, job.NextAttemptAt);
            return;
        }

        Complete(job, InvitationJobStatus.Failed, code, now);
        auditLogs.Add(AuditEntries.ForSystem(AuditActions.InvitationSendFailed, job.OrganizationId, job.UserId, now));
        await SaveIgnoringConflictAsync(job.Id, cancellationToken);
        logger.LogWarning(
            "Invitation job {JobId} for user {UserId} failed permanently with {ErrorCode} ({ExceptionType}) after {Attempt} attempts",
            job.Id, job.UserId, code, exception.ExceptionType, job.Attempts);
    }

    /// <summary>Delay before the next attempt; <paramref name="attemptsSoFar"/> ≥ 1. The last value repeats.</summary>
    private int RetryDelayMinutes(int attemptsSoFar)
    {
        var delays = Options.RetryDelaysMinutes;
        return delays.Length == 0 ? 1 : delays[Math.Min(attemptsSoFar, delays.Length) - 1];
    }

    private static void Complete(InvitationEmailJob job, InvitationJobStatus status, string? errorCode, DateTime now)
    {
        job.Status = status;
        job.LastErrorCode = errorCode;
        job.CompletedAt = now;
        job.LockedUntil = null;
    }

    /// <summary>A conflict here means a resend / reset cancelled the job meanwhile – its outcome no longer matters.</summary>
    private async Task SaveIgnoringConflictAsync(int jobId, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictException)
        {
            logger.LogInformation("Invitation job {JobId} was cancelled meanwhile; its result is not saved", jobId);
        }
    }
}
