using Common.DTOs.Invitations;
using Common.Options;
using Microsoft.Extensions.Options;
using Services.Interfaces;

namespace API.BackgroundJobs;

/// <summary>
/// Sends the queued invitation e-mails (InvitationEmailJobs). Each round: claim the due jobs of all organizations in a
/// plain scope, then process every job in a scope bound to its own organization. All logic is in
/// <see cref="IInvitationEmailProcessor"/>; this class only schedules.
/// A job that throws unexpectedly stays Processing; its lock expires and it is claimed again (Attempts counts it).
/// </summary>
public sealed class InvitationEmailWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<InvitationOptions> options,
    ILogger<InvitationEmailWorker> logger) : PollingBackgroundService(scopeFactory, logger)
{
    protected override TimeSpan Interval => TimeSpan.FromSeconds(options.Value.WorkerIntervalSeconds);

    protected override async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ClaimedInvitationJob> jobs;
        await using (var scope = ScopeFactory.CreateAsyncScope())
        {
            jobs = await scope.ServiceProvider.GetRequiredService<IInvitationEmailProcessor>().ClaimDueAsync(cancellationToken);
        }

        foreach (var job in jobs)
        {
            try
            {
                await using var scope = ScopeFactory.CreateOrganizationScope(job.OrganizationId);
                await scope.ServiceProvider.GetRequiredService<IInvitationEmailProcessor>()
                    .ProcessAsync(job.JobId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError("Invitation job {JobId} failed unexpectedly: {ExceptionType}; it will be retried",
                    job.JobId, exception.GetType().Name);
            }
        }
    }
}
