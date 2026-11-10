namespace Common.Enums;

/// <summary>Outcome of validating an AI response (spec 5.5). Routing decisions are made by the caller.</summary>
public enum AiValidationVerdict
{
    /// <summary>Well-formed, known category, confidence at or above the minimum.</summary>
    Valid = 1,

    /// <summary>Not valid JSON / does not match the schema – treat as a failure and retry.</summary>
    Invalid = 2,

    /// <summary>Well-formed, but the category code is not in the active list – route to GENERAL.</summary>
    UnknownCategory = 3,

    /// <summary>Well-formed, but confidence is below the minimum – route to GENERAL, keep the analysis as a suggestion.</summary>
    LowConfidence = 4
}
