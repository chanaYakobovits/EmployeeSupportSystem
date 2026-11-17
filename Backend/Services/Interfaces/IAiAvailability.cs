namespace Services.Interfaces;

public enum AiAvailability
{
    Available,

    /// <summary><c>Ai:Enabled = false</c> or no API key (spec 5.6).</summary>
    Disabled,

    /// <summary>The circuit breaker is open (spec 5.6).</summary>
    CircuitOpen
}

/// <summary>Whether a new ticket gets an analysis job, or goes to GENERAL right away (spec 5.6).</summary>
public interface IAiAvailability
{
    AiAvailability Check();
}
