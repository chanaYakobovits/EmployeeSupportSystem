using Common.Options;
using Microsoft.Extensions.Options;
using Services.Interfaces;

namespace Services.Implementations.Ai;

/// <inheritdoc />
public sealed class AiAvailabilityCheck(IOptions<AiOptions> options, IAiCircuitBreaker breaker) : IAiAvailability
{
    public AiAvailability Check()
    {
        var ai = options.Value;
        if (!ai.Enabled || string.IsNullOrWhiteSpace(ai.ApiKey))
        {
            return AiAvailability.Disabled;
        }

        return breaker.IsOpen ? AiAvailability.CircuitOpen : AiAvailability.Available;
    }
}
