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

    /// <summary>
    /// The "פתוחות" card of the requester's summary (spec 7.3): not closed or cancelled, and not waiting for the
    /// requester (WaitingForEmployee has its own card). Resolved counts as open until the employee confirms or it
    /// closes automatically.
    /// </summary>
    public static readonly IReadOnlyList<TicketStatus> RequesterOpen =
        [TicketStatus.New, TicketStatus.Assigned, TicketStatus.InProgress, TicketStatus.Resolved];
}
