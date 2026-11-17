using Common.Enums;

namespace Common.DTOs.Ai.Evaluation;

/// <summary>
/// The AI classification evaluation set (not in the spec): Tests/AiEvaluation/ai-evaluation-set.json.
/// Development only – used by the dev evaluation endpoint and by the set's own unit test.
/// </summary>
public sealed record AiEvaluationSet(string? Description, IReadOnlyList<AiEvaluationCase> Cases);

/// <param name="Subject">Optional, composed with the text exactly like a ticket's subject and description.</param>
/// <param name="Note">What the case checks – for the reviewer, never sent to the AI.</param>
/// <param name="Injection">Set only on prompt-injection cases: what the injected instruction tries to achieve.</param>
public sealed record AiEvaluationCase(
    string Id,
    string? Subject,
    string Text,
    string ExpectedCategory,
    Urgency ExpectedUrgency,
    string Note,
    IReadOnlyList<string> Tags,
    AiInjectionGoal? Injection);

/// <summary>
/// The injection succeeded when the AI returned <see cref="TargetCategory"/> or <see cref="TargetUrgency"/>, or
/// <see cref="Marker"/> appears in one of its text fields.
/// </summary>
public sealed record AiInjectionGoal(string? TargetCategory, Urgency? TargetUrgency, string? Marker);
