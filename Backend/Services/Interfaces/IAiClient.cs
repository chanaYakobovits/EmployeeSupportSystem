using Common.DTOs.Ai;

namespace Services.Interfaces;

/// <summary>Low-level AI call: one analysis request, request-level resilience only.</summary>
public interface IAiClient
{
    /// <summary>
    /// Analyzes a ticket text. Never throws for AI/network failures – the outcome is always in the result.
    /// Throws <see cref="OperationCanceledException"/> only when <paramref name="cancellationToken"/> is cancelled,
    /// and <see cref="ArgumentException"/> for empty input (a programming error).
    /// </summary>
    /// <param name="redactedText">
    /// Ticket text after <see cref="IPiiRedactor"/>. Sent to OpenAI as-is, nothing else from the ticket – in particular
    /// never its attachments, their names or their content (spec 4.3, 5.1). Do not add a file parameter here.
    /// </param>
    /// <param name="categories">Active categories; the response's categoryCode is restricted to their codes.</param>
    Task<AiAnalysisResult> AnalyzeAsync(
        string redactedText, IReadOnlyList<AiCategory> categories, CancellationToken cancellationToken);
}
