using Common.Enums;

namespace Common.Constants;

/// <summary>Groups of ticket statuses, defined in one place.</summary>
public static class TicketStatuses
{
    /// <summary>
    /// Statuses in which the SLA clock runs (decision in CLAUDE.md). WaitingForEmployee is excluded: its clock is paused.
    /// </summary>
    public static readonly IReadOnlyList<TicketStatus> Open =
        [TicketStatus.New, TicketStatus.Assigned, TicketStatus.InProgress];

    /// <summary>
    /// Not yet resolved, closed or cancelled: <see cref="Open"/> + WaitingForEmployee.
    /// A deactivated handler's tickets in these statuses go back to the department queue (task 2.3).
    /// </summary>
    public static readonly IReadOnlyList<TicketStatus> Unresolved =
        [TicketStatus.New, TicketStatus.Assigned, TicketStatus.InProgress, TicketStatus.WaitingForEmployee];
}
