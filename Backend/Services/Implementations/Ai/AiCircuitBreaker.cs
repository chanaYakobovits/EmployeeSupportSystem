using Common.Interfaces;
using Common.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Services.Interfaces;

namespace Services.Implementations.Ai;

/// <inheritdoc />
public sealed class AiCircuitBreaker(
    IDateTimeProvider clock,
    IOptions<AiOptions> options,
    ILogger<AiCircuitBreaker> logger) : IAiCircuitBreaker
{
    private readonly Lock _lock = new();
    private int _consecutiveFailures;
    private DateTime? _openUntil;

    public bool IsOpen
    {
        get
        {
            lock (_lock)
            {
                return _openUntil is { } until && clock.UtcNow < until;
            }
        }
    }

    public void RecordSuccess()
    {
        lock (_lock)
        {
            if (_openUntil is not null)
            {
                logger.LogInformation("AI circuit breaker closed after a successful call");
            }

            _consecutiveFailures = 0;
            _openUntil = null;
        }
    }

    public void RecordFailure()
    {
        lock (_lock)
        {
            _consecutiveFailures++;
            var now = clock.UtcNow;
            var alreadyOpen = _openUntil is { } until && now < until;
            // Also after the open period: the counter is still at the threshold, so a failed trial reopens at once.
            if (alreadyOpen || _consecutiveFailures < options.Value.CircuitBreakerFailures)
            {
                return;
            }

            _openUntil = now.AddMinutes(options.Value.CircuitBreakerMinutes);
            logger.LogWarning(
                "AI circuit breaker opened after {Failures} consecutive failures; no AI calls until {OpenUntil:o}",
                _consecutiveFailures, _openUntil);
        }
    }
}
