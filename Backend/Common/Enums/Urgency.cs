namespace Common.Enums;

/// <summary>Ticket urgency (spec 3.4). Values ascend with severity so ordering by urgency works.</summary>
public enum Urgency : byte
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
