using Common.DTOs.Ai;

namespace Services.Interfaces;

/// <summary>
/// The work of the AI background worker (spec 5.2), one job at a time. The worker only schedules: it claims in a plain
/// scope and processes each job in a scope bound to the job's organization.
/// </summary>
public interface IAiAnalysisProcessor
{
    /// <summary>Claims the due jobs of every organization (ids only). See IAiAnalysisJobRepository.ClaimDueJobsAsync.</summary>
    Task<IReadOnlyList<ClaimedAiJob>> ClaimDueAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Redacts, analyzes, validates, saves the analysis and classifies / routes the ticket – or schedules a retry, or
    /// falls back to GENERAL. Must run in a scope bound to the job's organization.
    /// </summary>
    Task ProcessAsync(int jobId, CancellationToken cancellationToken);
}
