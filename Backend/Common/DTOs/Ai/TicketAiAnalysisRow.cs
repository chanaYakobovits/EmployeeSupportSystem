using Common.Enums;

namespace Common.DTOs.Ai;

/// <summary>What the AI analysis endpoint reads in one query (task 4.4); shaped into the DTO by the service.</summary>
public sealed record TicketAiAnalysisRow(
    AiStatus AiStatus,
    int? CategoryId,
    Urgency? Urgency,
    AiJobRow? LatestJob,
    AiAnalysisRow? Analysis);

public sealed record AiJobRow(AiJobStatus Status, string? LastErrorCode, DateTime CreatedAt, DateTime NextAttemptAt);

/// <param name="CategoryId">The organization's category with this code, active or not; null when none.</param>
public sealed record AiAnalysisRow(
    string CategoryCode,
    int? CategoryId,
    string? CategoryName,
    bool CategoryIsActive,
    Urgency Urgency,
    decimal Confidence,
    string SuggestedSubject,
    string Summary,
    string MissingInfoJson,
    string SuggestedSolution,
    string Model,
    string PromptVersion,
    DateTime CreatedAt,
    AiValidationVerdict Verdict,
    AiAnalysisDisposition Disposition,
    string RedactedInput);
