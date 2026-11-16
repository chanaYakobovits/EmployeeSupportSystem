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
            TicketActions.TransferToDepartment], actions);
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

        Assert.Equal([TicketActions.InternalNote, TicketActions.Reclassify, TicketActions.Assign], actions);
    }

    [Fact]
    public void The_executive_only_views()
    {
        Assert.Empty(For(Viewer(Role.Executive), Ticket(assignedDepartment: OtherDepartment)));
    }

    [Fact]
    public void The_requester_view_has_no_staff_actions()
    {
        Assert.Empty(For(Viewer(Role.Handler), Ticket(requester: Me, assignedTo: null)));
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

    private static IReadOnlyList<string> For(TicketViewer viewer, TicketAccessFacts ticket) =>
        TicketAllowedActions.For(TicketAccessPolicy.Evaluate(viewer, ticket), ticket, viewer);

    private static TicketViewer Viewer(Role role, int? managed = null) =>
        new(Me, MyDepartment, managed, [Role.Employee, role]);

    private static TicketAccessFacts Ticket(int requester = Someone, int? assignedDepartment = MyDepartment,
        int? assignedTo = Me, TicketStatus status = TicketStatus.InProgress) =>
        new(1000, requester, 30, assignedDepartment, assignedTo, status);
}
