using Common.DTOs.Tickets;
using Common.Options;
using Microsoft.Extensions.Options;
using Services.Interfaces;

namespace API.BackgroundJobs;

/// <summary>
/// Closes resolved tickets 7 days after the resolution (spec 3.3, 10.2, 14.9). Each round: the due tickets of all
/// organizations (ids only) in a plain scope, then each ticket in a scope bound to its own organization – one ticket, one
/// save, so a conflict with the employee's action skips only that ticket. All logic is in <see cref="IAutoCloseService"/>;
/// this class only schedules. Registered only when AutoClose:Enabled.
/// </summary>
public sealed class AutoCloseResolvedWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<AutoCloseOptions> options,
    ILogger<AutoCloseResolvedWorker> logger) : PollingBackgroundService(scopeFactory, logger)
{
    protected override TimeSpan Interval => TimeSpan.FromMinutes(options.Value.IntervalMinutes);

    protected override async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<TicketRef> due;
        await using (var scope = ScopeFactory.CreateAsyncScope())
        {
            due = await scope.ServiceProvider.GetRequiredService<IAutoCloseService>()
                .GetDueAsync(options.Value.BatchSize, cancellationToken);
        }

        if (due.Count == 0)
        {
            return;
        }

        int closed = 0, skipped = 0;
        foreach (var ticket in due)
        {
            try
            {
                await using var scope = ScopeFactory.CreateOrganizationScope(ticket.OrganizationId);
                var done = await scope.ServiceProvider.GetRequiredService<IAutoCloseService>()
                    .TryCloseAsync(ticket.TicketId, cancellationToken);
                if (done)
                {
                    closed++;
                }
                else
                {
                    skipped++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Still Resolved: it is picked up again in the next round.
                skipped++;
                logger.LogError("Auto-close of ticket {TicketId} failed: {ExceptionType}",
                    ticket.TicketId, exception.GetType().Name);
            }
        }

        logger.LogInformation("Auto-close round: {Closed} tickets closed, {Skipped} skipped", closed, skipped);
    }
}
