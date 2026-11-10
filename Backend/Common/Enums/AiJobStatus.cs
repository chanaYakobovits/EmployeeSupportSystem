namespace Common.Enums;

/// <summary>State of a background AI analysis job (spec 8.3).</summary>
public enum AiJobStatus : byte
{
    Pending = 1,
    Processing = 2,
    Succeeded = 3,
    Failed = 4
}
