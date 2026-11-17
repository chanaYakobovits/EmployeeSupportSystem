namespace Services.Interfaces;

/// <summary>
/// Spec 5.6: after <c>Ai:CircuitBreakerFailures</c> consecutive failed AI calls, no AI calls for
/// <c>Ai:CircuitBreakerMinutes</c>. One instance per process (Singleton), shared by the worker and ticket creation.
/// After the open period the next call is a trial: success closes the breaker, failure opens it again at once.
/// </summary>
/// <remarks>Known limit: the state is per server, not shared between instances.</remarks>
public interface IAiCircuitBreaker
{
    bool IsOpen { get; }

    void RecordSuccess();

    /// <summary>A failed call (transient or rejected). <c>Disabled</c> is not a call and must not be recorded.</summary>
    void RecordFailure();
}
