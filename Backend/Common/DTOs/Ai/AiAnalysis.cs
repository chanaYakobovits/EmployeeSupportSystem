using Common.Enums;

namespace Common.DTOs.Ai;

/// <summary>
/// A validated AI analysis in the spec 5.4 format, with field lengths already trimmed to <see cref="AiFieldLimits"/>.
/// </summary>
/// <remarks>Contains AI output – must not be logged (spec 6.4). <see cref="ToString"/> omits the text fields.</remarks>
public sealed record AiAnalysis(
    string CategoryCode,
    Urgency Urgency,
    decimal Confidence,
    string SuggestedSubject,
    string Summary,
    string SuggestedSolution,
    IReadOnlyList<string> MissingInformation)
{
    public override string ToString() =>
        $"{nameof(AiAnalysis)} {{ CategoryCode = {CategoryCode}, Urgency = {Urgency}, Confidence = {Confidence} }}";
}
