namespace Common.Enums;

/// <summary>
/// The urgency filter of the staff lists: an <see cref="Urgency"/> (same values), or <see cref="NotSet"/> for tickets
/// whose urgency was not set yet (the fallback path, until a handler sets it).
/// </summary>
public enum UrgencyFilter : byte
{
    NotSet = 0,
    Low = Urgency.Low,
    Medium = Urgency.Medium,
    High = Urgency.High,
    Critical = Urgency.Critical
}
