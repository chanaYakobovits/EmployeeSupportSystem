using Common.DTOs.Ai;
using Common.Enums;
using Services.Interfaces;

namespace Services.Tests.Ai.Fakes;

/// <summary>
/// The AI client of the tests: returns the queued results in order (the last one repeats) and records exactly what it
/// was sent.
/// </summary>
public sealed class FakeAiClient : IAiClient
{
    private readonly Queue<AiAnalysisResult> _results = new();
    private AiAnalysisResult? _last;

    public List<string> SentTexts { get; } = [];
    public List<IReadOnlyList<AiCategory>> SentCategories { get; } = [];

    public int Calls => SentTexts.Count;

    /// <summary>Runs during the call – what other users do while the AI is working.</summary>
    public Action? OnCall { get; set; }

    public FakeAiClient Returns(params AiAnalysisResult[] results)
    {
        foreach (var result in results)
        {
            _results.Enqueue(result);
        }

        return this;
    }

    public Task<AiAnalysisResult> AnalyzeAsync(string redactedText, IReadOnlyList<AiCategory> categories,
        CancellationToken cancellationToken)
    {
        SentTexts.Add(redactedText);
        SentCategories.Add(categories);
        OnCall?.Invoke();
        _last = _results.Count > 0 ? _results.Dequeue() : _last ?? throw new InvalidOperationException("No result queued.");
        return Task.FromResult(_last);
    }

    // ---- results ----

    public const string Model = "gpt-4o-mini-test";
    public const string PromptVersion = "test-1";

    public static AiAnalysisResult Analysis(string categoryCode, Urgency urgency = Urgency.High, decimal confidence = 0.9m,
        AiValidationVerdict verdict = AiValidationVerdict.Valid) =>
        AiAnalysisResult.Success(
            new AiAnalysis(categoryCode, urgency, confidence, "תלוש שכר חסר", "העובד לא קיבל תלוש",
                "שלום, נבדוק ונחזור אלייך.", ["חודש התלוש"]),
            verdict, Model, PromptVersion, inputTokens: 321, outputTokens: 123, durationMs: 1500, attempts: 1);

    public static AiAnalysisResult Failure(AiErrorCode code) =>
        AiAnalysisResult.Failure(code, Model, PromptVersion, durationMs: 30000, attempts: 2);
}
