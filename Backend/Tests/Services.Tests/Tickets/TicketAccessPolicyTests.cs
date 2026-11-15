using Common.DTOs.Tickets;
using Common.Enums;
using Services.Implementations.Tickets;

namespace Services.Tests.Tickets;

/// <summary>The permission matrix of spec 2.4, role by role, without a database.</summary>
public sealed class TicketAccessPolicyTests
{
    private const int Me = 1;
    private const int Someone = 2;
    private const int MyDepartment = 10;
    private const int OtherDepartment = 20;
    private const int ThirdDepartment = 30;

    // ---- requester ----

    [Fact]
    public void The_requester_sees_and_replies_but_never_sees_or_writes_internal_notes()
    {
        var permissions = Evaluate(Viewer(), Ticket(requester: Me));

        Assert.Equal(new TicketPermissions(CanView: true, IsRequesterView: true, CanSeeInternal: false, CanReply: true,
            CanWriteInternal: false), permissions);
    }

    [Fact]
    public void An_employee_has_no_access_to_another_employees_ticket_even_in_the_same_department()
    {
        var permissions = Evaluate(Viewer(), Ticket(requester: Someone, requesterDepartment: MyDepartment));

        Assert.Equal(TicketPermissions.None, permissions);
    }

    // ---- handler ----

    [Fact]
    public void A_handler_sees_a_ticket_assigned_to_them_with_internal_notes()
    {
        var permissions = Evaluate(Viewer(Role.Handler), Ticket(assignedTo: Me, assignedDepartment: OtherDepartment));

        Assert.Equal(new TicketPermissions(CanView: true, IsRequesterView: false, CanSeeInternal: true, CanReply: true,
            CanWriteInternal: true), permissions);
    }

    [Fact]
    public void A_handler_sees_the_queue_and_every_ticket_of_their_handling_department()
    {
        var queued = Evaluate(Viewer(Role.Handler), Ticket(assignedDepartment: MyDepartment));
        var colleagues = Evaluate(Viewer(Role.Handler), Ticket(assignedDepartment: MyDepartment, assignedTo: Someone));

        Assert.True(queued is { CanView: true, CanReply: true, CanWriteInternal: true, CanSeeInternal: true });
        Assert.True(colleagues is { CanView: true, CanReply: true, CanWriteInternal: true, CanSeeInternal: true });
    }

    [Fact]
    public void A_handler_has_no_access_to_another_departments_ticket()
    {
        Assert.Equal(TicketPermissions.None,
            Evaluate(Viewer(Role.Handler), Ticket(assignedDepartment: OtherDepartment, assignedTo: Someone)));
    }

    [Fact]
    public void A_handler_does_not_see_tickets_opened_by_employees_of_their_own_department()
    {
        // The handler's scope is the tickets the department handles, not those its employees opened.
        Assert.Equal(TicketPermissions.None,
            Evaluate(Viewer(Role.Handler), Ticket(requesterDepartment: MyDepartment, assignedDepartment: OtherDepartment)));
    }

    [Fact]
    public void A_ticket_still_assigned_to_a_user_who_lost_the_handler_role_is_not_theirs_anymore()
    {
        Assert.Equal(TicketPermissions.None,
            Evaluate(Viewer(), Ticket(assignedTo: Me, assignedDepartment: MyDepartment)));
    }

    [Fact]
    public void A_new_ticket_not_yet_routed_is_in_no_handlers_queue()
    {
        Assert.Equal(TicketPermissions.None, Evaluate(Viewer(Role.Handler), Ticket(assignedDepartment: null)));
    }

    // ---- department manager ----

    [Fact]
    public void A_manager_sees_tickets_of_their_departments_employees_writes_internal_notes_but_does_not_reply()
    {
        var permissions = Evaluate(Viewer(Role.DepartmentManager, managed: MyDepartment),
            Ticket(requesterDepartment: MyDepartment, assignedDepartment: OtherDepartment));

        Assert.Equal(new TicketPermissions(CanView: true, IsRequesterView: false, CanSeeInternal: true, CanReply: false,
            CanWriteInternal: true), permissions);
    }

    [Fact]
    public void A_manager_sees_tickets_their_department_handles()
    {
        var permissions = Evaluate(Viewer(Role.DepartmentManager, managed: MyDepartment),
            Ticket(requesterDepartment: OtherDepartment, assignedDepartment: MyDepartment));

        Assert.True(permissions is { CanView: true, CanSeeInternal: true, CanWriteInternal: true, CanReply: false });
    }

    [Fact]
    public void A_manager_has_no_access_to_other_departments_tickets()
    {
        Assert.Equal(TicketPermissions.None, Evaluate(Viewer(Role.DepartmentManager, managed: MyDepartment),
            Ticket(requesterDepartment: OtherDepartment, assignedDepartment: ThirdDepartment)));
    }

    [Fact]
    public void The_manager_role_without_a_managed_department_gives_no_scope()
    {
        Assert.Equal(TicketPermissions.None, Evaluate(Viewer(Role.DepartmentManager, managed: null),
            Ticket(requesterDepartment: MyDepartment)));
    }

    // ---- executive ----

    [Fact]
    public void An_executive_sees_every_ticket_with_internal_notes_but_writes_nothing()
    {
        var permissions = Evaluate(Viewer(Role.Executive),
            Ticket(requesterDepartment: ThirdDepartment, assignedDepartment: OtherDepartment));

        Assert.Equal(new TicketPermissions(CanView: true, IsRequesterView: false, CanSeeInternal: true, CanReply: false,
            CanWriteInternal: false), permissions);
    }

    // ---- requester who is also staff ----

    [Theory]
    [InlineData(Role.Handler)]
    [InlineData(Role.DepartmentManager)]
    [InlineData(Role.Executive)]
    public void A_staff_member_who_opened_the_ticket_gets_the_requester_view(Role role)
    {
        var permissions = Evaluate(Viewer(role, managed: MyDepartment),
            Ticket(requester: Me, requesterDepartment: MyDepartment, assignedDepartment: MyDepartment));

        Assert.Equal(new TicketPermissions(CanView: true, IsRequesterView: true, CanSeeInternal: false, CanReply: true,
            CanWriteInternal: false), permissions);
    }

    private static TicketPermissions Evaluate(TicketViewer viewer, TicketAccessFacts ticket) =>
        TicketAccessPolicy.Evaluate(viewer, ticket);

    private static TicketViewer Viewer(Role? role = null, int? managed = null) =>
        new(Me, MyDepartment, managed, role is { } r ? [Role.Employee, r] : [Role.Employee]);

    private static TicketAccessFacts Ticket(int requester = Someone, int requesterDepartment = ThirdDepartment,
        int? assignedDepartment = OtherDepartment, int? assignedTo = null) =>
        new(1000, requester, requesterDepartment, assignedDepartment, assignedTo, TicketStatus.InProgress);
}
