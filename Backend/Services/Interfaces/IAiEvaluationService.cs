using Common.DTOs.Ai.Evaluation;

namespace Services.Interfaces;

/// <summary>
/// Development only (not in the spec): measures the AI classification through the background worker's path
/// (organization names, PiiRedactor, IAiClient with its validator, the same decision rule). Never writes to the
/// database, never routes, never touches the circuit breaker.
/// </summary>
public interface IAiEvaluationService
{
    /// <summary>One text, with the signed-in user as the requester (for name redaction) and their organization.</summary>
    Task<DevAiAnalyzeResultDto> AnalyzeAsync(DevAiAnalyzeRequest request, CancellationToken cancellationToken);

    /// <summary>The whole evaluation set, one case after the other (never in parallel).</summary>
    Task<AiEvaluationReportDto> EvaluateAsync(CancellationToken cancellationToken);
}
