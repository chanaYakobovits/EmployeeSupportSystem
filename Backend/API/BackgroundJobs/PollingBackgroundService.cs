namespace API.BackgroundJobs;

/// <summary>
/// Base for the database-queue workers (invitation e-mails now, AI analysis in task 4.3): one round every
/// <see cref="Interval"/>, each round in its own DI scope(s) (spec 10.3 – the worker is a singleton, the DbContext must
/// be fresh). A failed round is logged and the loop goes on; the queue rows themselves keep the state, so nothing is
/// lost. Only the exception type is logged – messages may contain data (e.g. a duplicate key value).
/// </summary>
public abstract class PollingBackgroundService(IServiceScopeFactory scopeFactory, ILogger logger) : BackgroundService
{
    protected IServiceScopeFactory ScopeFactory => scopeFactory;

    protected abstract TimeSpan Interval { get; }

    /// <summary>One round. Opens its own scopes (see <see cref="ServiceScopeFactoryExtensions"/>).</summary>
    protected abstract Task RunOnceAsync(CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("{Worker} started, interval {Interval}", GetType().Name, Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError("{Worker} round failed: {ExceptionType}", GetType().Name, exception.GetType().Name);
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("{Worker} stopped", GetType().Name);
    }
}
