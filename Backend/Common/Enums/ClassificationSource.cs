namespace Common.Enums;

/// <summary>Who determined the ticket's category and urgency (spec 8.3).</summary>
public enum ClassificationSource : byte
{
    Ai = 1,
    Fallback = 2,
    Manual = 3
}
