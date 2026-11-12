using System.Data;
using Common.DTOs.Invitations;
using Common.Enums;
using Common.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Repositories.Data;
using Repositories.Interfaces;

namespace Repositories.Implementations;

public sealed class UserInvitationRepository(AppDbContext db) : IUserInvitationRepository
{
    public void AddJob(InvitationEmailJob job) => db.InvitationEmailJobs.Add(job);

    public void AddInvitation(UserInvitation invitation) => db.UserInvitations.Add(invitation);

    public Task<int> CancelOpenJobsAsync(int userId, DateTime now, CancellationToken cancellationToken) =>
        db.InvitationEmailJobs
            .Where(j => j.UserId == userId &&
                        (j.Status == InvitationJobStatus.Pending || j.Status == InvitationJobStatus.Processing))
            .ExecuteUpdateAsync(set => set
                .SetProperty(j => j.Status, InvitationJobStatus.Cancelled)
                .SetProperty(j => j.LockedUntil, (DateTime?)null)
                .SetProperty(j => j.CompletedAt, now), cancellationToken);

    public Task<int> RevokeOpenInvitationsAsync(int userId, DateTime now, CancellationToken cancellationToken) =>
        db.UserInvitations
            .Where(i => i.UserId == userId && i.UsedAt == null && i.RevokedAt == null && i.ExpiresAt > now)
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.RevokedAt, now), cancellationToken);

    public async Task<IReadOnlyList<ClaimedInvitationJob>> ClaimDueJobsAsync(int batchSize, DateTime now,
        DateTime lockedUntil, CancellationToken cancellationToken)
    {
        // One statement: select the due rows (skipping rows another server has locked) and mark them in place.
        // Not composed with LINQ, so EF sends it as is.
        const string sql = """
            WITH due AS (
                SELECT TOP (@batchSize) *
                FROM [InvitationEmailJobs] WITH (ROWLOCK, READPAST, UPDLOCK)
                WHERE ([Status] = @pending AND [NextAttemptAt] <= @now)
                   OR ([Status] = @processing AND [LockedUntil] < @now)
                ORDER BY [NextAttemptAt], [Id]
            )
            UPDATE due
            SET [Status] = @processing, [LockedUntil] = @lockedUntil, [Attempts] = [Attempts] + 1
            OUTPUT inserted.[Id] AS [JobId], inserted.[OrganizationId] AS [OrganizationId];
            """;

        var rows = await db.Database
            .SqlQueryRaw<ClaimedJobRow>(sql,
                new SqlParameter("@batchSize", SqlDbType.Int) { Value = batchSize },
                new SqlParameter("@pending", SqlDbType.TinyInt) { Value = (byte)InvitationJobStatus.Pending },
                new SqlParameter("@processing", SqlDbType.TinyInt) { Value = (byte)InvitationJobStatus.Processing },
                new SqlParameter("@now", SqlDbType.DateTime2) { Value = now },
                new SqlParameter("@lockedUntil", SqlDbType.DateTime2) { Value = lockedUntil })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new ClaimedInvitationJob(r.JobId, r.OrganizationId)).ToList();
    }

    public Task<InvitationEmailJob?> FindJobAsync(int jobId, CancellationToken cancellationToken) =>
        db.InvitationEmailJobs
            .Include(j => j.Invitation)
            .SingleOrDefaultAsync(j => j.Id == jobId, cancellationToken);

    // IgnoreQueryFilters: set-password is anonymous, so the organization filter would hide the (required) User and
    // with it the invitation. Same reasoning as RefreshTokenRepository.FindByHashAsync.
    public Task<UserInvitation?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.UserInvitations
            .IgnoreQueryFilters()
            .Include(i => i.User).ThenInclude(u => u.Roles)
            .SingleOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

    public async Task<bool> TryMarkUsedAsync(int invitationId, DateTime now, CancellationToken cancellationToken) =>
        await db.UserInvitations
            .Where(i => i.Id == invitationId && i.UsedAt == null && i.RevokedAt == null && i.ExpiresAt > now)
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.UsedAt, now), cancellationToken) == 1;

    /// <summary>Shape of the OUTPUT clause of the claim statement.</summary>
    private sealed class ClaimedJobRow
    {
        public int JobId { get; set; }
        public int OrganizationId { get; set; }
    }
}
