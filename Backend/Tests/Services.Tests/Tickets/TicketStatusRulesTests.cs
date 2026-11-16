using Common.Enums;
using Common.Exceptions;
using Common.Models;
using Services.Implementations.Tickets;

namespace Services.Tests.Tickets;

/// <summary>
/// Spec 3.3, 11.5: every allowed transition passes, every other one is rejected – the full table of (from, to, actor),
/// written out here independently of <see cref="TicketStatusRules.Transitions"/>.
/// </summary>
public sealed class TicketStatusRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>Spec 3.3 as written, plus the system's InProgress → Assigned on a handler change.</summary>
    private static readonly HashSet<(TicketStatus From, TicketStatus To, StatusActor Actor)> Expected =
    [
        (TicketStatus.New, TicketStatus.Assigned, StatusActor.System),
        (TicketStatus.New, TicketStatus.Cancelled, StatusActor.Requester),
        (TicketStatus.Assigned, TicketStatus.Cancelled, StatusActor.Requester),
        (TicketStatus.Assigned, TicketStatus.InProgress, StatusActor.Handler),
        (TicketStatus.InProgress, TicketStatus.WaitingForEmployee, StatusActor.Handler),
        (TicketStatus.InProgress, TicketStatus.Resolved, StatusActor.Handler),
        (TicketStatus.WaitingForEmployee, TicketStatus.InProgress, StatusActor.System),
        (TicketStatus.Resolved, TicketStatus.Closed, StatusActor.Requester),
        (TicketStatus.Resolved, TicketStatus.Closed, StatusActor.System),
        (TicketStatus.Resolved, TicketStatus.InProgress, StatusActor.Requester),
        (TicketStatus.InProgress, TicketStatus.Assigned, StatusActor.System),
    ];

    public static TheoryData<TicketStatus, TicketStatus, StatusActor> AllCombinations()
    {
        var data = new TheoryData<TicketStatus, TicketStatus, StatusActor>();
        foreach (var from in Enum.GetValues<TicketStatus>())
        foreach (var to in Enum.GetValues<TicketStatus>())
        foreach (var actor in Enum.GetValues<StatusActor>())
        {
            data.Add(from, to, actor);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void Exactly_the_transitions_of_spec_3_3_are_allowed(TicketStatus from, TicketStatus to, StatusActor actor) =>
        Assert.Equal(Expected.Contains((from, to, actor)), TicketStatusRules.IsAllowed(from, to, actor));

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void Applying_a_transition_succeeds_or_is_rejected_with_422(TicketStatus from, TicketStatus to,
        StatusActor actor)
    {
        var ticket = new Ticket { Status = from, CreatedAt = Now.AddDays(-2), ResolvedAt = Now.AddDays(-1) };
        Dictionary<Urgency, int> hours = new() { [Urgency.Medium] = 72 };

        if (Expected.Contains((from, to, actor)))
        {
            TicketStatusTransitions.Apply(ticket, to, actor, 5, null, Now, hours);
            Assert.Equal(to, ticket.Status);
            var row = Assert.Single(ticket.StatusHistory);
            Assert.Equal((from, to, (int?)5, Now), (row.PreviousStatus!.Value, row.NewStatus, row.ChangedByUserId, row.ChangedAt));
        }
        else
        {
            var error = Assert.Throws<BusinessRuleException>(() =>
                TicketStatusTransitions.Apply(ticket, to, actor, 5, null, Now, hours));
            Assert.Equal("invalid-status-transition", error.ErrorType);
            Assert.Equal(from, ticket.Status);
            Assert.Empty(ticket.StatusHistory);
        }
    }

    [Fact]
    public void Every_user_transition_has_an_action_and_no_system_transition_has_one()
    {
        Assert.All(TicketStatusRules.Transitions, t =>
            Assert.Equal(t.Actor != StatusActor.System, t.Action is not null));
    }

    [Fact]
    public void The_rejection_names_both_statuses_in_hebrew()
    {
        var error = TicketStatusTransitions.Invalid(TicketStatus.Assigned, TicketStatus.Resolved);

        Assert.Equal("לא ניתן להעביר פנייה בסטטוס \"שויכה\" לסטטוס \"נפתרה\"", error.Title);
    }

    [Fact]
    public void Reopening_is_allowed_until_just_before_7_days_and_closing_from_exactly_7_days()
    {
        var resolved = Now.AddDays(-7);

        Assert.True(TicketStatusRules.CanReopen(resolved, Now.AddMinutes(-1)));
        Assert.False(TicketStatusRules.IsAutoCloseDue(resolved, Now.AddMinutes(-1)));

        Assert.False(TicketStatusRules.CanReopen(resolved, Now));
        Assert.True(TicketStatusRules.IsAutoCloseDue(resolved, Now));

        Assert.False(TicketStatusRules.CanReopen(null, Now));
        Assert.False(TicketStatusRules.IsAutoCloseDue(null, Now));
    }

    // ===================== What goes with a transition =====================

    [Fact]
    public void Waiting_pauses_the_sla_clock_and_resuming_moves_the_due_time_by_the_wait()
    {
        var due = Now.AddHours(10);
        var ticket = new Ticket { Status = TicketStatus.InProgress, CreatedAt = Now.AddDays(-1), DueAt = due };

        TicketStatusTransitions.Apply(ticket, TicketStatus.WaitingForEmployee, StatusActor.Handler, 5, null, Now);
        Assert.Equal(Now, ticket.SlaPausedAt);

        TicketStatusTransitions.Apply(ticket, TicketStatus.InProgress, StatusActor.System, null, null, Now.AddDays(3));

        Assert.Equal(due.AddDays(3), ticket.DueAt);
        Assert.Equal(3 * 24 * 3600, ticket.SlaPausedSeconds);
        Assert.Null(ticket.SlaPausedAt);
        Assert.Null(ticket.StatusHistory.Last().ChangedByUserId);
    }

    [Fact]
    public void Resolving_and_closing_set_their_times()
    {
        var ticket = new Ticket { Status = TicketStatus.InProgress };

        TicketStatusTransitions.Apply(ticket, TicketStatus.Resolved, StatusActor.Handler, 5, " נימוק ", Now);
        TicketStatusTransitions.Apply(ticket, TicketStatus.Closed, StatusActor.Requester, 1, null, Now.AddDays(1));

        Assert.Equal((Now, Now.AddDays(1)), (ticket.ResolvedAt!.Value, ticket.ClosedAt!.Value));
        Assert.Equal("נימוק", ticket.StatusHistory.First().Reason);
        Assert.Equal(Now.AddDays(1), ticket.UpdatedAt);
    }

    [Fact]
    public void Reopening_starts_a_new_clock_from_now_by_the_urgency_or_medium()
    {
        var ticket = new Ticket
        {
            Status = TicketStatus.Resolved, CreatedAt = Now.AddDays(-10), ResolvedAt = Now.AddDays(-2),
            SlaPausedSeconds = 500, DueAt = Now.AddDays(-7), Urgency = null,
        };

        TicketStatusTransitions.Apply(ticket, TicketStatus.InProgress, StatusActor.Requester, 1, null, Now,
            new Dictionary<Urgency, int> { [Urgency.Medium] = 72 });

        Assert.Equal(Now.AddHours(72), ticket.DueAt);
        Assert.Equal((Now, 0), (ticket.SlaRestartedAt!.Value, ticket.SlaPausedSeconds));
        Assert.Null(ticket.ResolvedAt);
    }
}
