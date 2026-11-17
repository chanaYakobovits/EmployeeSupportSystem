using Common.DTOs.Ai;
using Common.Models;

namespace Repositories.Interfaces;

/// <summary>
/// The AI analysis queue (<see cref="AiAnalysisJob"/>, spec 5.2) and the stored analyses (<see cref="TicketAiAnalysis"/>).
/// New jobs are staged with their ticket (<c>Ticket.AiAnalysisJobs</c>), in the ticket's transaction (spec 4.3).
/// </summary>
public interface IAiAnalysisJobRepository
{
    /// <summary>
    /// Atomically claims up to <paramref name="batchSize"/> due jobs of all organizations (Pending with
    /// NextAttemptAt &lt;= now, or Processing with an expired lock – a server that crashed): Status → Processing,
    /// LockedUntil, Attempts + 1. Rows locked by another server are skipped (READPAST), so a job is never claimed twice.
    /// </summary>
    Task<IReadOnlyList<ClaimedAiJob>> ClaimDueJobsAsync(int batchSize, DateTime now, DateTime lockedUntil,
        CancellationToken cancellationToken);

    /// <summary>Tracked job, or null. Attempts is its concurrency token.</summary>
    Task<AiAnalysisJob?> FindJobAsync(int jobId, CancellationToken cancellationToken);

    /// <summary>Tracked analysis of the ticket (at most one – TicketId is unique), or null.</summary>
    Task<TicketAiAnalysis?> FindAnalysisAsync(int ticketId, CancellationToken cancellationToken);

    /// <summary>Stages an analysis; written by <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
    void AddAnalysis(TicketAiAnalysis analysis);

    // ===================== Task 4.4: the handler's view and "נתח מחדש" =====================

    /// <summary>
    /// The ticket's AI state, its latest job and its stored analysis (with the category of its code), in one no-tracking
    /// query. The scope was checked before; the ticket goes through the organization filter. Null if not found.
    /// </summary>
    Task<TicketAiAnalysisRow?> GetAnalysisViewAsync(int ticketId, CancellationToken cancellationToken);

    /// <summary>When the ticket's latest analysis job was created, or null.</summary>
    Task<DateTime?> GetLatestJobCreatedAtAsync(int ticketId, CancellationToken cancellationToken);

    /// <summary>Stages a job; written by <see cref="IUnitOfWork.SaveChangesAsync"/> with the ticket.</summary>
    void AddJob(AiAnalysisJob job);
}
