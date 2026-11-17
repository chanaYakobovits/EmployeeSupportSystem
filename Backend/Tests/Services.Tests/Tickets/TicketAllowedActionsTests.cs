using Common.Constants;
using Common.DTOs.Tickets;
using Common.Enums;
using Services.Implementations.Tickets;

namespace Services.Tests.Tickets;

/// <summary>
/// The allowedActions of the staff view (task 3.4): the permission matrix narrowed by the status and the assignment, with
/// the same rules as the endpoints – without a database.
/// </summary>
public sealed class TicketAllowedActionsTests
{
    private const int Me = 1;
    private const int Someone = 2;
    private const int MyDepartment = 10;
    private const int OtherDepartment = 20;

    [Fact]
    public void A_handler_with_a_personal_ticket_in_progress_may_do_everything_but_take_it()
    {
        var actions = For(Viewer(Role.Handler), Ticket(assignedTo: Me));

        Assert.Equal([TicketActions.Reply, TicketActions.InternalNote, TicketActions.Reclassify, TicketActions.Assign,
            TicketActions.TransferToDepartment, TicketActions.ViewAiAnalysis, TicketActions.RequestInfo,
            TicketActions.Resolve], actions);
    }

    [Fact]
    public void A_handler_may_take_a_ticket_from_the_queue_of_their_department()
    {
        var actions = For(Viewer(Role.Handler), Ticket(assignedTo: null));

        Assert.Contains(TicketActions.TakeFromQueue, actions);
    }

    [Fact]
    public void A_ticket_of_a_colleague_cannot_be_taken_from_the_queue()
    {
        var actions = For(Viewer(Role.Handler), Ticket(assignedTo: Someone));

        Assert.DoesNotContain(TicketActions.TakeFromQueue, actions);
        Assert.Contains(TicketActions.Assign, actions);
    }

    [Fact]
    public void The_manager_of_the_handling_department_may_note_reclassify_and_assign_but_not_reply_move_or_take()
    {
        var actions = For(Viewer(Role.DepartmentManager, managed: OtherDepartment),
            Ticket(assignedDepartment: OtherDepartment, assignedTo: null));

        Assert.Equal([TicketActions.InternalNote, TicketActions.Reclassify, TicketActions.Assign,
            TicketActions.ViewAiAnalysis], actions);
    }

    [Fact]
    public void The_executive_only_views()
    {
        Assert.Equal([TicketActions.ViewAiAnalysis], For(Viewer(Role.Executive), Ticket(assignedDepartment: OtherDepartment)));
    }

    [Fact]
    public void The_requester_view_has_no_staff_actions()
    {
        // Their own ticket in progress: only a reply (task 3.5 – the requester's actions, none of the staff's).
        Assert.Equal([TicketActions.Reply], For(Viewer(Role.Handler), Ticket(requester: Me, assignedTo: null)));
    }

    [Theory]
    [InlineData(TicketStatus.WaitingForEmployee, true, true)]
    [InlineData(TicketStatus.Resolved, true, false)]
    [InlineData(TicketStatus.Closed, false, false)]
    [InlineData(TicketStatus.Cancelled, false, false)]
    public void The_status_limits_comments_and_changes(TicketStatus status, bool comments, bool changes)
    {
        var actions = For(Viewer(Role.Handler), Ticket(assignedTo: null, status: status));

        Assert.Equal(comments, actions.Contains(TicketActions.Reply));
        Assert.Equal(comments, actions.Contains(TicketActions.InternalNote));
        Assert.Equal(changes, actions.Contains(TicketActions.Reclassify));
        Assert.Equal(changes, actions.Contains(TicketActions.Assign));
        Assert.Equal(changes, actions.Contains(TicketActions.TransferToDepartment));
        Assert.Equal(changes, actions.Contains(TicketActions.TakeFromQueue));
    }

    [Fact]
    public void A_ticket_that_was_not_routed_can_be_reclassified_but_not_assigned()
    {
        // Assigned to the handler personally, without a handling department (not reachable by routing, but by data).
        var actions = For(Viewer(Role.Handler), Ticket(assignedDepartment: null, assignedTo: Me, status: TicketStatus.New));

        Assert.Contains(TicketActions.Reclassify, actions);
        Assert.DoesNotContain(TicketActions.Assign, actions);
        Assert.DoesNotContain(TicketActions.TransferToDepartment, actions);
        Assert.DoesNotContain(TicketActions.TakeFromQueue, actions);
    }

    // ===================== AI (task 4.4) =====================

    [Theory]
    [InlineData(AiStatus.Completed, true)]
    [InlineData(AiStatus.Failed, true)]
    [InlineData(AiStatus.Skipped, true)]
    [InlineData(AiStatus.Pending, false)]
    [InlineData(AiStatus.Processing, false)]
    public void A_handler_may_retry_an_analysis_that_is_not_in_progress(AiStatus aiStatus, bool allowed)
    {
        var actions = For(Viewer(Role.Handler), Ticket(ai: Ai(aiStatus)));

        Assert.Equal(allowed, actions.Contains(TicketActions.RetryAnalysis));
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public void No_retry_or_apply_once_the_ticket_is_resolved(TicketStatus status)
    {
        var actions = For(Viewer(Role.Handler), Ticket(status: status, ai: Ai(recommended: 7)));

        Assert.Contains(TicketActions.ViewAiAnalysis, actions);
        Assert.DoesNotContain(TicketActions.RetryAnalysis, actions);
        Assert.DoesNotContain(TicketActions.ApplyRecommendation, actions);
    }

    [Fact]
    public void Managers_and_the_executive_never_retry()
    {
        var manager = For(Viewer(Role.DepartmentManager, managed: OtherDepartment),
            Ticket(assignedDepartment: OtherDepartment, assignedTo: null, ai: Ai(recommended: 7)));
        var executive = For(Viewer(Role.Executive), Ticket(assignedDepartment: OtherDepartment, ai: Ai(recommended: 7)));

        Assert.DoesNotContain(TicketActions.RetryAnalysis, manager);
        // The manager of the handling department reclassifies – so may apply the recommendation; the executive only views.
        Assert.Contains(TicketActions.ApplyRecommendation, manager);
        Assert.Equal([TicketActions.ViewAiAnalysis], executive);
    }

    [Theory]
    [InlineData(5, Urgency.High, 7, Urgency.High, true)]   // another category
    [InlineData(7, Urgency.Low, 7, Urgency.High, true)]    // another urgency
    [InlineData(7, null, 7, Urgency.High, true)]           // the fallback left the urgency empty
    [InlineData(7, Urgency.High, 7, Urgency.High, false)]  // already the recommendation
    [InlineData(5, Urgency.High, null, Urgency.High, false)] // no analysis / its category is not active
    public void Apply_is_offered_only_for_an_active_recommendation_that_differs(int? category, Urgency? urgency,
        int? recommended, Urgency? recommendedUrgency, bool offered)
    {
        var actions = For(Viewer(Role.Handler), Ticket(ai: Ai(category: category, urgency: urgency,
            recommended: recommended, recommendedUrgency: recommendedUrgency)));

        Assert.Equal(offered, actions.Contains(TicketActions.ApplyRecommendation));
    }

    [Fact]
    public void The_requester_view_has_no_ai_actions()
    {
        var actions = For(Viewer(Role.Handler), Ticket(requester: Me, ai: Ai(recommended: 7)));

        Assert.DoesNotContain(TicketActions.ViewAiAnalysis, actions);
        Assert.DoesNotContain(TicketActions.RetryAnalysis, actions);
        Assert.DoesNotContain(TicketActions.ApplyRecommendation, actions);
    }

    private static TicketAiFacts Ai(AiStatus status = AiStatus.Completed, int? category = 5,
        Urgency? urgency = Urgency.Medium, int? recommended = null, Urgency? recommendedUrgency = Urgency.High) =>
        new(status, category, urgency, recommended, recommended is null ? null : recommendedUrgency);

    private static IReadOnlyList<string> For(TicketViewer viewer, TicketAccessFacts ticket) =>
        TicketAllowedActions.For(TicketAccessPolicy.Evaluate(viewer, ticket), ticket, viewer, DateTime.UtcNow);

    private static TicketViewer Viewer(Role role, int? managed = null) =>
        new(Me, MyDepartment, managed, [Role.Employee, role]);

    private static TicketAccessFacts Ticket(int requester = Someone, int? assignedDepartment = MyDepartment,
        int? assignedTo = Me, TicketStatus status = TicketStatus.InProgress, TicketAiFacts? ai = null) =>
        new(1000, requester, 30, assignedDepartment, assignedTo, status, Ai: ai);
}
