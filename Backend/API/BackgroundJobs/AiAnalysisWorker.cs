using Common.DTOs.Ai;
using Common.Options;
using Microsoft.Extensions.Options;
using Services.Interfaces;

namespace API.BackgroundJobs;

/// <summary>
/// AI analysis of new tickets (spec 5.2). Every <c>Ai:WorkerIntervalSeconds</c>: claim the due jobs of all organizations
/// in a plain scope, then process them in parallel, each in a scope bound to its own organization (spec 10.3). In
/// parallel so no job's lock runs out while it waits for the rest of the batch. All logic is in
/// <see cref="IAiAnalysisProcessor"/>; this class only schedules.
/// A job that throws unexpectedly stays Processing; its lock expires and it is claimed again (Attempts counts it).
/// </summary>
public sealed class AiAnalysisWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<AiOptions> options,
    ILogger<AiAnalysisWorker> logger) : PollingBackgroundService(scopeFactory, logger)
{
    protected override TimeSpan Interval => TimeSpan.FromSeconds(options.Value.WorkerIntervalSeconds);

    protected override async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ClaimedAiJob> jobs;
        await using (var scope = ScopeFactory.CreateAsyncScope())
        {
            jobs = await scope.ServiceProvider.GetRequiredService<IAiAnalysisProcessor>().ClaimDueAsync(cancellationToken);
        }

        await Task.WhenAll(jobs.Select(job => ProcessAsync(job, cancellationToken)));
    }

    private async Task ProcessAsync(ClaimedAiJob job, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = ScopeFactory.CreateOrganizationScope(job.OrganizationId);
            await scope.ServiceProvider.GetRequiredService<IAiAnalysisProcessor>().ProcessAsync(job.JobId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down: the job stays Processing and is claimed again after its lock expires.
        }
        catch (Exception exception)
        {
            logger.LogError("AI job {JobId} failed unexpectedly: {ExceptionType}; it will be retried",
                job.JobId, exception.GetType().Name);
        }
    }
}
