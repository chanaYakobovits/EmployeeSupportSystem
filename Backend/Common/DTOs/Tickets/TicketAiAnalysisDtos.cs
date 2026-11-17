using Common.Enums;

namespace Common.DTOs.Tickets;

/// <summary>
/// GET /api/tickets/{id}/ai-analysis (task 4.4, spec 7.7 area 2): the state of the analysis and the latest stored one.
/// Staff only – the requester's DTOs never carry any of this.
/// </summary>
/// <param name="Analysis">The latest stored analysis, or null when none succeeded yet. A failed or running re-analysis
/// keeps the previous one (it is replaced only by a successful one).</param>
public sealed record TicketAiAnalysisDto(AiProcessingDto Processing, AiAnalysisDetailsDto? Analysis);

/// <param name="Status">The ticket's AiStatus: Pending / Processing / Completed / Failed / Skipped.</param>
/// <param name="ErrorCode">The latest job's error code, when it failed or waits for a retry; never content.</param>
/// <param name="ErrorMessage">The error (or Skipped) in Hebrew, for the screen.</param>
/// <param name="RequestedAt">When the latest analysis job was created (the opening, or "נתח מחדש").</param>
/// <param name="NextAttemptAt">When a Pending job will be tried (a retry after a transient failure).</param>
public sealed record AiProcessingDto(
    AiStatus Status,
    string? ErrorCode,
    string? ErrorMessage,
    DateTime? RequestedAt,
    DateTime? NextAttemptAt);

/// <param name="CategoryName">The category of the code in the organization; null when the code matches none.</param>
/// <param name="CategoryId">Only when that category is active – the one "החל המלצה" sends.</param>
/// <param name="ConfidenceLevel">High / Medium / Low – by <c>Ai:MinConfidence</c> and <see cref="AiConfidence.High"/>.</param>
/// <param name="Applied">The AI's classification was applied to the ticket when it was saved (Disposition Applied).</param>
/// <param name="MatchesCurrentClassification">The ticket's category and urgency are the recommended ones now.</param>
/// <param name="RedactedInput">Exactly what was sent to the AI, after PII redaction (spec 5.3).</param>
public sealed record AiAnalysisDetailsDto(
    string CategoryCode,
    string? CategoryName,
    int? CategoryId,
    Urgency Urgency,
    decimal Confidence,
    AiConfidenceLevel ConfidenceLevel,
    string SuggestedSubject,
    string Summary,
    IReadOnlyList<string> MissingInformation,
    string SuggestedSolution,
    string Model,
    string PromptVersion,
    DateTime CreatedAt,
    AiValidationVerdict Verdict,
    AiAnalysisDisposition Disposition,
    bool Applied,
    bool MatchesCurrentClassification,
    string RedactedInput);

public enum AiConfidenceLevel
{
    Low = 1,
    Medium = 2,
    High = 3
}

/// <summary>Bands of the confidence label (task 4.4). Below <c>Ai:MinConfidence</c> is Low.</summary>
public static class AiConfidence
{
    public const decimal High = 0.85m;

    public static AiConfidenceLevel LevelOf(decimal confidence, decimal minConfidence) =>
        confidence >= High ? AiConfidenceLevel.High
        : confidence >= minConfidence ? AiConfidenceLevel.Medium
        : AiConfidenceLevel.Low;
}
