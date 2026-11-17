namespace Common.Options;

/// <summary>
/// The development-only AI evaluation (not in the spec). Bound from the "AiEvaluation" configuration section.
/// </summary>
public sealed class AiEvaluationOptions
{
    public const string SectionName = "AiEvaluation";

    /// <summary>The evaluation set. A relative path is resolved against the API's content root.</summary>
    public string DatasetPath { get; set; } = "../Tests/AiEvaluation/ai-evaluation-set.json";

    /// <summary>
    /// USD per million input tokens, for the cost estimate only. Default: gpt-4o-mini – check against the current
    /// price list before relying on it.
    /// </summary>
    public decimal InputPricePerMillionUsd { get; set; } = 0.15m;

    /// <summary>USD per million output tokens, for the cost estimate only (default: gpt-4o-mini).</summary>
    public decimal OutputPricePerMillionUsd { get; set; } = 0.60m;
}
