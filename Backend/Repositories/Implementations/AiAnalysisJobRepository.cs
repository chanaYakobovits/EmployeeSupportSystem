using System.Data;
using Common.DTOs.Ai;
using Common.Enums;
using Common.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Repositories.Data;
using Repositories.Interfaces;

namespace Repositories.Implementations;

public sealed class AiAnalysisJobRepository(AppDbContext db) : IAiAnalysisJobRepository
{
    public async Task<IReadOnlyList<ClaimedAiJob>> ClaimDueJobsAsync(int batchSize, DateTime now, DateTime lockedUntil,
        CancellationToken cancellationToken)
    {
        // One statement: select the due rows (skipping rows another server has locked) and mark them in place.
        // Same pattern as the invitation queue. Not composed with LINQ, so EF sends it as is.
        // Index AiAnalysisJobs(Status, NextAttemptAt) – spec 8.4.
        const string sql = """
            WITH due AS (
                SELECT TOP (@batchSize) *
                FROM [AiAnalysisJobs] WITH (ROWLOCK, READPAST, UPDLOCK)
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
                new SqlParameter("@pending", SqlDbType.TinyInt) { Value = (byte)AiJobStatus.Pending },
                new SqlParameter("@processing", SqlDbType.TinyInt) { Value = (byte)AiJobStatus.Processing },
                new SqlParameter("@now", SqlDbType.DateTime2) { Value = now },
                new SqlParameter("@lockedUntil", SqlDbType.DateTime2) { Value = lockedUntil })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new ClaimedAiJob(r.JobId, r.OrganizationId)).ToList();
    }

    public Task<AiAnalysisJob?> FindJobAsync(int jobId, CancellationToken cancellationToken) =>
        db.AiAnalysisJobs.SingleOrDefaultAsync(j => j.Id == jobId, cancellationToken);

    public Task<TicketAiAnalysis?> FindAnalysisAsync(int ticketId, CancellationToken cancellationToken) =>
        db.TicketAiAnalyses.SingleOrDefaultAsync(a => a.TicketId == ticketId, cancellationToken);

    public void AddAnalysis(TicketAiAnalysis analysis) => db.TicketAiAnalyses.Add(analysis);

    public Task<TicketAiAnalysisRow?> GetAnalysisViewAsync(int ticketId, CancellationToken cancellationToken) =>
        // Starts from Tickets: the organization filter applies (the job table has none). Categories are filtered too.
        db.Tickets
            .AsNoTracking()
            .Where(t => t.Id == ticketId)
            .Select(t => new TicketAiAnalysisRow(
                t.AiStatus,
                t.CategoryId,
                t.Urgency,
                t.AiAnalysisJobs
                    .OrderByDescending(j => j.CreatedAt).ThenByDescending(j => j.Id)
                    .Select(j => new AiJobRow(j.Status, j.LastErrorCode, j.CreatedAt, j.NextAttemptAt))
                    .FirstOrDefault(),
                t.AiAnalysis == null
                    ? null
                    : new AiAnalysisRow(
                        t.AiAnalysis.CategoryCode,
                        db.Categories.Where(c => c.Code == t.AiAnalysis.CategoryCode).Select(c => (int?)c.Id)
                            .FirstOrDefault(),
                        db.Categories.Where(c => c.Code == t.AiAnalysis.CategoryCode).Select(c => c.Name)
                            .FirstOrDefault(),
                        db.Categories.Any(c => c.Code == t.AiAnalysis.CategoryCode && c.IsActive),
                        t.AiAnalysis.Urgency,
                        t.AiAnalysis.Confidence,
                        t.AiAnalysis.SuggestedSubject,
                        t.AiAnalysis.Summary,
                        t.AiAnalysis.MissingInfoJson,
                        t.AiAnalysis.SuggestedSolution,
                        t.AiAnalysis.Model,
                        t.AiAnalysis.PromptVersion,
                        t.AiAnalysis.CreatedAt,
                        t.AiAnalysis.Verdict,
                        t.AiAnalysis.Disposition,
                        t.AiAnalysis.RedactedInput)))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<DateTime?> GetLatestJobCreatedAtAsync(int ticketId, CancellationToken cancellationToken) =>
        db.AiAnalysisJobs
            .AsNoTracking()
            .Where(j => j.TicketId == ticketId)
            .MaxAsync(j => (DateTime?)j.CreatedAt, cancellationToken);

    public void AddJob(AiAnalysisJob job) => db.AiAnalysisJobs.Add(job);

    /// <summary>Shape of the OUTPUT clause of the claim statement.</summary>
    private sealed class ClaimedJobRow
    {
        public int JobId { get; set; }
        public int OrganizationId { get; set; }
    }
}
