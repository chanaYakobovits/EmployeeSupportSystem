using Common.Enums;

namespace Common.DTOs.Ai.Evaluation;

/// <summary>POST /api/dev/ai/analyze (development only): one text through the background worker's AI path.</summary>
public sealed record DevAiAnalyzeRequest(string? Subject, string Text);

/// <summary>
/// What the ticket would get: the AI's category (<see cref="Source"/> Ai) or the fallback (Fallback) – after a
/// low confidence, an unknown category or a failed call.
/// </summary>
public sealed record AiDecisionDto(string? CategoryCode, ClassificationSource Source);

/// <summary>
/// The outcome of one text. <see cref="RedactedText"/> is exactly what was sent (null when redaction failed and
/// nothing was sent). <see cref="ErrorCode"/>: an <see cref="AiErrorCode"/> name or RedactionFailed.
/// </summary>
public sealed record DevAiAnalyzeResultDto(
    string? RedactedText,
    AiAnalysis? Analysis,
    AiValidationVerdict? Verdict,
    string? ErrorCode,
    AiDecisionDto Decision,
    string Model,
    string PromptVersion,
    int InputTokens,
    int OutputTokens,
    int DurationMs);

/// <summary>POST /api/dev/ai/evaluate: the whole evaluation set, run one case after the other.</summary>
/// <param name="CategoryAccuracy">Percent of cases whose decision (what the ticket would get) is the expected category.</param>
/// <param name="RawCategoryAccuracy">Percent of cases where the AI itself chose the expected category.</param>
/// <param name="UrgencyExact">Percent of cases with the AI's urgency equal to the expected one.</param>
/// <param name="UrgencyWithinOne">Percent of cases with the AI's urgency at most one level away.</param>
/// <param name="FailedCalls">Cases without an analysis (failed call or redaction) – counted as wrong everywhere.</param>
public sealed record AiEvaluationReportDto(
    DateTime RunAt,
    string Model,
    string PromptVersion,
    int CaseCount,
    int CategoryCorrect,
    decimal CategoryAccuracy,
    int RawCategoryCorrect,
    decimal RawCategoryAccuracy,
    int UrgencyExactCount,
    decimal UrgencyExact,
    int UrgencyWithinOneCount,
    decimal UrgencyWithinOne,
    int FailedCalls,
    IReadOnlyList<AiEvaluationMistakeDto> Mistakes,
    IReadOnlyList<AiInjectionResultDto> Injections,
    int TotalInputTokens,
    int TotalOutputTokens,
    decimal EstimatedCostUsd,
    int DurationMs);

/// <summary>A case whose decision category or urgency differs from the expected one, or whose call failed.</summary>
public sealed record AiEvaluationMistakeDto(
    string Id,
    string Note,
    string? Subject,
    string Text,
    string ExpectedCategory,
    Urgency ExpectedUrgency,
    string? ReceivedCategory,
    Urgency? ReceivedUrgency,
    decimal? Confidence,
    AiValidationVerdict? Verdict,
    AiDecisionDto Decision,
    string? ErrorCode,
    bool CategoryWrong,
    bool UrgencyWrong);

/// <summary>Whether a prompt-injection case got what the injected instruction asked for.</summary>
public sealed record AiInjectionResultDto(
    string Id,
    bool Succeeded,
    bool CategoryHijacked,
    bool UrgencyHijacked,
    bool MarkerFound,
    string? ErrorCode);
