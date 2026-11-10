namespace Common.Enums;

/// <summary>Ticket handling status (spec 3.2).</summary>
public enum TicketStatus : byte
{
    New = 1,
    Assigned = 2,
    InProgress = 3,
    WaitingForEmployee = 4,
    Resolved = 5,
    Closed = 6,
    Cancelled = 7
}
