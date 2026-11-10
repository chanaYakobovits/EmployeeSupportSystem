using Common.Enums;

namespace Common.DTOs.Ai;

/// <summary>
/// Outcome of one AI analysis call. Always one of: success (with <see cref="Analysis"/> and <see cref="Verdict"/>),
/// transient failure (<see cref="IsTransient"/> – worth retrying later) or final failure.
/// </summary>
/// <remarks><see cref="ToString"/> contains metadata only and is safe to log (spec 6.4).</remarks>
public sealed record AiAnalysisResult
{
    public bool IsSuccess => ErrorCode is null;

    public AiErrorCode? ErrorCode { get; init; }

    /// <summary>True when a later retry may succeed (timeout, rate limit, server error, invalid response).</summary>
    public bool IsTransient => ErrorCode is AiErrorCode.Timeout or AiErrorCode.RateLimited
        or AiErrorCode.ServerError or AiErrorCode.InvalidResponse;

    /// <summary>Set on success only.</summary>
    public AiAnalysis? Analysis { get; init; }

    /// <summary>Set on success only: Valid, UnknownCategory or LowConfidence.</summary>
    public AiValidationVerdict? Verdict { get; init; }

    /// <summary>Model reported by OpenAI, or the configured model when no response was received.</summary>
    public string Model { get; init; } = string.Empty;

    public string PromptVersion { get; init; } = string.Empty;
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public int DurationMs { get; init; }

    /// <summary>HTTP attempts made (0 when disabled).</summary>
    public int Attempts { get; init; }

    /// <summary>HTTP status of the last failed attempt, when there was one.</summary>
    public int? HttpStatus { get; init; }

    public static AiAnalysisResult Success(
        AiAnalysis analysis, AiValidationVerdict verdict, string model, string promptVersion,
        int inputTokens, int outputTokens, int durationMs, int attempts) => new()
    {
        Analysis = analysis,
        Verdict = verdict,
        Model = model,
        PromptVersion = promptVersion,
        InputTokens = inputTokens,
        OutputTokens = outputTokens,
        DurationMs = durationMs,
        Attempts = attempts
    };

    public static AiAnalysisResult Failure(
        AiErrorCode errorCode, string model, string promptVersion, int durationMs, int attempts,
        int? httpStatus = null, int inputTokens = 0, int outputTokens = 0) => new()
    {
        ErrorCode = errorCode,
        Model = model,
        PromptVersion = promptVersion,
        DurationMs = durationMs,
        Attempts = attempts,
        HttpStatus = httpStatus,
        InputTokens = inputTokens,
        OutputTokens = outputTokens
    };

    public override string ToString() =>
        $"{nameof(AiAnalysisResult)} {{ ErrorCode = {ErrorCode}, Verdict = {Verdict}, Model = {Model}, " +
        $"PromptVersion = {PromptVersion}, Attempts = {Attempts}, HttpStatus = {HttpStatus}, " +
        $"InputTokens = {InputTokens}, OutputTokens = {OutputTokens}, DurationMs = {DurationMs} }}";
}
