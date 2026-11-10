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

    /// <summary>Attempts per analysis job – used by the background worker, not by the client.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Sampling temperature (0..2). Null (empty in configuration) means the parameter is not sent at all –
    /// needed for models that reject it.
    /// </summary>
    public float? Temperature { get; set; } = 0.2f;

    /// <summary>Inclusive minimum: confidence equal to this value is accepted.</summary>
    public decimal MinConfidence { get; set; } = 0.6m;

    public int WorkerIntervalSeconds { get; set; } = 5;

    public int BatchSize { get; set; } = 10;

    public int FallbackAfterMinutes { get; set; } = 15;
}
