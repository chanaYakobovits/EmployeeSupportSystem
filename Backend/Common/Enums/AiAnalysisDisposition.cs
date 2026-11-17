namespace Common.Enums;

/// <summary>
/// What became of a stored AI analysis when it was saved (task 4.4): applied to the ticket, or kept as a recommendation
/// only – and why. Recorded at that moment: a later manual reclassification does not change it.
/// </summary>
public enum AiAnalysisDisposition : byte
{
    /// <summary>The AI's category and urgency were applied to the ticket (ClassificationSource Ai).</summary>
    Applied = 1,

    /// <summary>Confidence below <c>Ai:MinConfidence</c> – the ticket went to GENERAL (spec 5.5).</summary>
    LowConfidence = 2,

    /// <summary>The category code is not one of the active categories (or was deactivated meanwhile) – GENERAL.</summary>
    UnknownCategory = 3,

    /// <summary>A handler had already classified the ticket (Manual) – their decision stays (spec 11.2).</summary>
    ManuallyClassified = 4,

    /// <summary>The ticket was no longer New (e.g. "נתח מחדש" on a routed ticket) – the routing stays (spec 11.2).</summary>
    AlreadyRouted = 5
}
