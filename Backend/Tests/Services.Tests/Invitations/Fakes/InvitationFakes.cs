using Common.DTOs.Invitations;
using Common.Enums;
using Common.Models;
using Repositories.Interfaces;
using Services.Interfaces;
using Services.Tests.Auth.Fakes;

namespace Services.Tests.Invitations.Fakes;

/// <summary>
/// In-memory invitations and queue, with the same semantics as the SQL of UserInvitationRepository
/// (conditional updates, claim of due / stale jobs). <see cref="StatusOf"/> mirrors the status projection of
/// UserRepository, so service responses can be checked end to end.
/// </summary>
public sealed class FakeUserInvitationRepository(FakeUserRepository users) : IUserInvitationRepository
{
    private int _nextJobId = 1;
    private int _nextInvitationId = 1;

    public List<InvitationEmailJob> Jobs { get; } = [];

    public List<UserInvitation> Invitations { get; } = [];

    public void AddJob(InvitationEmailJob job)
    {
        job.Id = _nextJobId++;
        if (job.User is not null)
        {
            job.UserId = job.User.Id;
        }

        Jobs.Add(job);
    }

    public void AddInvitation(UserInvitation invitation)
    {
        invitation.Id = _nextInvitationId++;
        Invitations.Add(invitation);
    }

    public Task<int> CancelOpenJobsAsync(int userId, DateTime now, CancellationToken cancellationToken)
    {
        var open = Jobs.Where(j => j.UserId == userId &&
                                   j.Status is InvitationJobStatus.Pending or InvitationJobStatus.Processing).ToList();
        foreach (var job in open)
        {
            job.Status = InvitationJobStatus.Cancelled;
            job.LockedUntil = null;
            job.CompletedAt = now;
        }

        return Task.FromResult(open.Count);
    }

    public Task<int> RevokeOpenInvitationsAsync(int userId, DateTime now, CancellationToken cancellationToken)
    {
        var open = Invitations.Where(i => i.UserId == userId && IsValid(i, now)).ToList();
        foreach (var invitation in open)
        {
            invitation.RevokedAt = now;
        }

        return Task.FromResult(open.Count);
    }

    public Task<IReadOnlyList<ClaimedInvitationJob>> ClaimDueJobsAsync(int batchSize, DateTime now, DateTime lockedUntil,
        CancellationToken cancellationToken)
    {
        var due = Jobs
            .Where(j => (j.Status == InvitationJobStatus.Pending && j.NextAttemptAt <= now) ||
                        (j.Status == InvitationJobStatus.Processing && j.LockedUntil < now))
            .OrderBy(j => j.NextAttemptAt).ThenBy(j => j.Id)
            .Take(batchSize)
            .ToList();
        foreach (var job in due)
        {
            job.Status = InvitationJobStatus.Processing;
            job.LockedUntil = lockedUntil;
            job.Attempts++;
        }

        return Task.FromResult<IReadOnlyList<ClaimedInvitationJob>>(
            due.Select(j => new ClaimedInvitationJob(j.Id, j.OrganizationId)).ToList());
    }

    public Task<InvitationEmailJob?> FindJobAsync(int jobId, CancellationToken cancellationToken) =>
        Task.FromResult(Jobs.SingleOrDefault(j => j.Id == jobId));

    public Task<UserInvitation?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        var invitation = Invitations.SingleOrDefault(i => i.TokenHash == tokenHash);
        if (invitation is not null)
        {
            invitation.User = users.Users.Single(u => u.Id == invitation.UserId);
        }

        return Task.FromResult(invitation);
    }

    /// <summary>Simulates another request whose conditional UPDATE marked the link used first.</summary>
    public bool LoseNextMarkUsed { get; set; }

    /// <summary>The one conditional UPDATE: only the first caller wins.</summary>
    public Task<bool> TryMarkUsedAsync(int invitationId, DateTime now, CancellationToken cancellationToken)
    {
        if (LoseNextMarkUsed)
        {
            LoseNextMarkUsed = false;
            var other = Invitations.Single(i => i.Id == invitationId);
            other.UsedAt = now;
            return Task.FromResult(false);
        }

        var invitation = Invitations.SingleOrDefault(i => i.Id == invitationId && IsValid(i, now));
        if (invitation is null)
        {
            return Task.FromResult(false);
        }

        invitation.UsedAt = now;
        return Task.FromResult(true);
    }

    public InvitationStatusDto? StatusOf(int userId, DateTime now)
    {
        var job = Jobs
            .Where(j => j.UserId == userId && j.Status != InvitationJobStatus.Cancelled)
            .OrderByDescending(j => j.CreatedAt).ThenByDescending(j => j.Id)
            .FirstOrDefault();
        if (job is null)
        {
            return null;
        }

        return job.Status switch
        {
            InvitationJobStatus.Pending or InvitationJobStatus.Processing => new(InvitationStatus.Pending, job.CreatedAt),
            InvitationJobStatus.Failed => new(InvitationStatus.Failed, job.CompletedAt ?? job.CreatedAt),
            _ when job.Invitation!.UsedAt is { } usedAt => new(InvitationStatus.Used, usedAt),
            _ when job.Invitation!.RevokedAt is { } revokedAt => new(InvitationStatus.Revoked, revokedAt),
            _ when job.Invitation!.ExpiresAt <= now => new(InvitationStatus.Expired, job.Invitation.ExpiresAt),
            _ => new(InvitationStatus.Sent, job.CompletedAt ?? job.Invitation!.CreatedAt),
        };
    }

    public IEnumerable<UserInvitation> ValidFor(int userId, DateTime now) =>
        Invitations.Where(i => i.UserId == userId && IsValid(i, now));

    private static bool IsValid(UserInvitation invitation, DateTime now) =>
        invitation.UsedAt is null && invitation.RevokedAt is null && invitation.ExpiresAt > now;
}

/// <summary>Records every message. Can fail the next sends with a given code.</summary>
public sealed class CapturingEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    /// <summary>The following sends throw this (until set back to null).</summary>
    public EmailSendException? FailWith { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (FailWith is { } failure)
        {
            throw failure;
        }

        Sent.Add(message);
        return Task.CompletedTask;
    }

    /// <summary>The token from the link in the last message (what the employee would click).</summary>
    public string LastToken()
    {
        const string marker = "#token=";
        var text = Sent[^1].TextBody;
        var start = text.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = text.IndexOfAny(['\r', '\n'], start);
        return text[start..(end < 0 ? text.Length : end)];
    }
}
