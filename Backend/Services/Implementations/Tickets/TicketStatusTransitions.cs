using Common.Enums;
using Common.Exceptions;
using Common.Models;
using Services.Implementations.Routing;

namespace Services.Implementations.Tickets;

/// <summary>
/// Makes a status transition on a tracked ticket – the only place that writes a <see cref="TicketStatusHistory"/> row
/// after the opening one. Checks <see cref="TicketStatusRules"/>, records who, when, from, to and the reason, sets
/// UpdatedAt and what goes with the transition: ResolvedAt / ClosedAt, pausing and resuming the SLA clock, a new clock on
/// a reopening. No SaveChanges: the caller saves everything in one transaction (spec 11.2).
/// </summary>
public static class TicketStatusTransitions
{
    public const string InvalidTransitionType = "invalid-status-transition";

    /// <param name="changedByUserId">Who caused it; null = the system (shown as "מערכת").</param>
    /// <param name="reason">Internal: shown in the staff timeline only.</param>
    /// <param name="slaHours">The organization's SLA hours – required for a reopening (a new due time).</param>
    /// <exception cref="BusinessRuleException">The transition is not in the table for this actor (422).</exception>
    public static void Apply(Ticket ticket, TicketStatus to, StatusActor actor, int? changedByUserId, string? reason,
        DateTime now, IReadOnlyDictionary<Urgency, int>? slaHours = null)
    {
        var from = ticket.Status;
        if (!TicketStatusRules.IsAllowed(from, to, actor))
        {
            throw Invalid(from, to);
        }

        if (from == TicketStatus.WaitingForEmployee)
        {
            ResumeSla(ticket, now);
        }

        switch (to)
        {
            case TicketStatus.WaitingForEmployee:
                // Spec 3.2: the SLA clock is paused while waiting for the employee.
                ticket.SlaPausedAt = now;
                break;
            case TicketStatus.Resolved:
                ticket.ResolvedAt = now;
                break;
            case TicketStatus.Closed:
                ticket.ClosedAt = now;
                break;
            case TicketStatus.InProgress when from == TicketStatus.Resolved:
                Reopen(ticket, now, slaHours ?? throw new ArgumentNullException(nameof(slaHours),
                    "A reopening needs the SLA hours."));
                break;
        }

        var trimmed = reason?.Trim();
        ticket.StatusHistory.Add(new TicketStatusHistory
        {
            PreviousStatus = from,
            NewStatus = to,
            ChangedByUserId = changedByUserId,
            ChangedAt = now,
            Reason = string.IsNullOrEmpty(trimmed) ? null : trimmed,
        });
        ticket.Status = to;
        ticket.UpdatedAt = now;
    }

    public static BusinessRuleException Invalid(TicketStatus from, TicketStatus to) =>
        new($"לא ניתן להעביר פנייה בסטטוס \"{TicketStatusRules.Label(from)}\" לסטטוס \"{TicketStatusRules.Label(to)}\"",
            errorType: InvalidTransitionType);

    /// <summary>The wait is over: its length is added to the paused time and to the due time.</summary>
    private static void ResumeSla(Ticket ticket, DateTime now)
    {
        if (ticket.SlaPausedAt is not { } pausedAt)
        {
            return;
        }

        var seconds = (int)Math.Max(0, Math.Floor((now - pausedAt).TotalSeconds));
        ticket.SlaPausedSeconds += seconds;
        ticket.DueAt = ticket.DueAt?.AddSeconds(seconds);
        ticket.SlaPausedAt = null;
    }

    /// <summary>
    /// A new clock from the reopening, by the urgency (none → Medium). The previous resolution stays in the history (the
    /// ChangedAt of its Resolved row); ResolvedAt is cleared until the next one.
    /// </summary>
    private static void Reopen(Ticket ticket, DateTime now, IReadOnlyDictionary<Urgency, int> slaHours)
    {
        ticket.ResolvedAt = null;
        ticket.SlaRestartedAt = now;
        ticket.SlaPausedSeconds = 0;
        ticket.SlaPausedAt = null;
        ticket.DueAt = SlaCalculator.DueAt(ticket, slaHours);
    }
}
