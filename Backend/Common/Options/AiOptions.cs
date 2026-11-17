namespace Common.Options;

/// <summary>AI settings (spec 5.7). Bound from the "Ai" configuration section.</summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>When false, every analysis returns <c>Disabled</c> without calling OpenAI (spec 5.6).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// OpenAI API key. Never stored in appsettings.json or git (spec 6.5):
    /// development – <c>dotnet user-secrets set "Ai:ApiKey" "..."</c>; server – environment variable <c>Ai__ApiKey</c>.
    /// Missing key behaves like <see cref="Enabled"/> = false; the application still starts.
    /// </summary>
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>Timeout of a single HTTP attempt.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Retries of an analysis job after its first attempt fails transiently (spec 4.3: "up to 3 attempts – after 30
    /// seconds, 2 minutes, 10 minutes" = first attempt + 3 retries). After that the ticket goes to GENERAL.
    /// Used by the background worker, not by the client.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Delay before retry n (1-based) of a job, in seconds (spec 4.3). The last value repeats.
    /// Empty here on purpose: the configuration binder appends a configured array to a non-empty default, so the
    /// default (<see cref="DefaultRetryDelaysSeconds"/>) is filled in after binding when nothing is configured.
    /// </summary>
    public int[] RetryDelaysSeconds { get; set; } = [];

    public static readonly int[] DefaultRetryDelaysSeconds = [30, 120, 600];

    /// <summary>
    /// Sampling temperature (0..2). Null (empty in configuration) means the parameter is not sent at all –
    /// needed for models that reject it.
    /// </summary>
    public float? Temperature { get; set; } = 0.2f;

    /// <summary>Inclusive minimum: confidence equal to this value is accepted.</summary>
    public decimal MinConfidence { get; set; } = 0.6m;

    public int WorkerIntervalSeconds { get; set; } = 5;

    public int BatchSize { get; set; } = 10;

    /// <summary>A ticket without a result this long after it was opened goes to GENERAL in any case (spec 5.6).</summary>
    public int FallbackAfterMinutes { get; set; } = 15;

    /// <summary>Consecutive failed AI calls that open the circuit breaker (spec 5.6).</summary>
    public int CircuitBreakerFailures { get; set; } = 5;

    /// <summary>How long the open circuit breaker blocks AI calls (spec 5.6).</summary>
    public int CircuitBreakerMinutes { get; set; } = 5;
}
