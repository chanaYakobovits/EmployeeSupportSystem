namespace Common.Enums;

/// <summary>State of the AI analysis of a ticket (spec 3.1).</summary>
public enum AiStatus : byte
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4
}
