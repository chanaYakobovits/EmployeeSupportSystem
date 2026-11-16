using Common.DTOs.Tickets;
using Common.Enums;
using Common.Models;
using Services.Implementations.Tickets;

namespace Services.Tests.Tickets;

/// <summary>
/// The staff lists' predicates against the single-ticket policy (spec 2.4), over every combination of roles and ticket
/// routing: a ticket is in one of the lists the user may request exactly when the policy lets them view it as staff.
/// </summary>
public sealed class TicketListScopesTests
{
    private const int Me = 1;
    private const int Someone = 2;
    private const int MyDepartment = 10;
    private const int OtherDepartment = 20;
    private const int ThirdDepartment = 30;

    private static readonly TicketListScope[] Scopes = Enum.GetValues<TicketListScope>();

    public static TheoryData<Role[], int?> Viewers()
    {
        var data = new TheoryData<Role[], int?>();
        Role[] staffRoles = [Role.Handler, Role.DepartmentManager, Role.Executive];
        for (var mask = 0; mask < 1 << staffRoles.Length; mask++)
        {
            var roles = staffRoles.Where((_, i) => (mask & (1 << i)) != 0).Prepend(Role.Employee).ToArray();
            // A manager manages their own department or another one; a manager without a department is invalid data.
            foreach (var managed in new int?[] { null, MyDepartment, OtherDepartment })
            {
                data.Add(roles, managed);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Viewers))]
    public void The_lists_hold_exactly_the_tickets_the_policy_lets_the_user_view_as_staff(Role[] roles, int? managed)
    {
        var viewer = new TicketViewer(Me, MyDepartment, managed, roles);
        var allowed = Scopes.Where(s => TicketListScopes.IsAllowed(viewer, s)).ToList();
        var predicates = allowed.Select(s => (Scope: s, Matches: TicketListScopes.Predicate(viewer, s).Compile())).ToList();

        foreach (var ticket in Tickets())
        {
            var permissions = TicketAccessPolicy.Evaluate(viewer, Facts(ticket));
            var viewsAsStaff = permissions is { CanView: true, IsRequesterView: false };
            var inLists = predicates.Where(p => p.Matches(ticket)).Select(p => p.Scope).ToList();

            if (inLists.Count > 0)
            {
                Assert.True(viewsAsStaff, $"{Describe(ticket)} is in {string.Join(",", inLists)} but not viewable as staff");
            }
            else
            {
                Assert.False(viewsAsStaff, $"{Describe(ticket)} is viewable as staff but in no list of [{string.Join(",", roles)}]");
            }
        }
    }

    [Fact]
    public void The_handler_lists_need_the_handler_role_and_the_department_list_a_manager_or_the_executive()
    {
        var employee = new TicketViewer(Me, MyDepartment, null, [Role.Employee]);
        var handler = new TicketViewer(Me, MyDepartment, null, [Role.Employee, Role.Handler]);
        var manager = new TicketViewer(Me, MyDepartment, MyDepartment, [Role.Employee, Role.DepartmentManager]);
        var executive = new TicketViewer(Me, MyDepartment, null, [Role.Employee, Role.Executive]);

        Assert.DoesNotContain(Scopes, s => TicketListScopes.IsAllowed(employee, s));
        Assert.Equal([TicketListScope.Assigned, TicketListScope.Queue, TicketListScope.Handling],
            Scopes.Where(s => TicketListScopes.IsAllowed(handler, s)));
        Assert.Equal([TicketListScope.Department], Scopes.Where(s => TicketListScopes.IsAllowed(manager, s)));
        Assert.Equal([TicketListScope.Department], Scopes.Where(s => TicketListScopes.IsAllowed(executive, s)));
    }

    [Fact]
    public void The_queue_holds_only_tickets_without_a_personal_handler()
    {
        var handler = new TicketViewer(Me, MyDepartment, null, [Role.Employee, Role.Handler]);
        var queue = TicketListScopes.Predicate(handler, TicketListScope.Queue).Compile();

        Assert.True(queue(Ticket(Someone, OtherDepartment, MyDepartment, assignedTo: null)));
        Assert.False(queue(Ticket(Someone, OtherDepartment, MyDepartment, assignedTo: Someone)));
        Assert.False(queue(Ticket(Someone, OtherDepartment, MyDepartment, assignedTo: Me)));
    }

    [Fact]
    public void The_users_own_tickets_are_in_no_staff_list()
    {
        var everything = new TicketViewer(Me, MyDepartment, MyDepartment,
            [Role.Employee, Role.Handler, Role.DepartmentManager, Role.Executive]);
        var mine = Ticket(Me, MyDepartment, MyDepartment, assignedTo: null);

        Assert.All(Scopes, s => Assert.False(TicketListScopes.Predicate(everything, s).Compile()(mine)));
    }

    [Fact]
    public void A_scope_that_is_not_allowed_has_no_predicate()
    {
        var employee = new TicketViewer(Me, MyDepartment, null, [Role.Employee]);

        Assert.Throws<InvalidOperationException>(() => TicketListScopes.Predicate(employee, TicketListScope.Queue));
    }

    private static IEnumerable<Ticket> Tickets()
    {
        int[] departments = [MyDepartment, OtherDepartment, ThirdDepartment];
        foreach (var requester in new[] { Me, Someone })
        foreach (var requesterDepartment in departments)
        foreach (var assignedDepartment in departments.Cast<int?>().Prepend(null))
        foreach (var assignedTo in new int?[] { null, Me, Someone })
        {
            yield return Ticket(requester, requesterDepartment, assignedDepartment, assignedTo);
        }
    }

    private static Ticket Ticket(int requester, int requesterDepartment, int? assignedDepartment, int? assignedTo) => new()
    {
        Id = 1,
        RequesterId = requester,
        RequesterDepartmentId = requesterDepartment,
        AssignedDepartmentId = assignedDepartment,
        AssignedToUserId = assignedTo,
        Status = TicketStatus.Assigned,
    };

    private static TicketAccessFacts Facts(Ticket t) => new(t.Id, t.RequesterId, t.RequesterDepartmentId,
        t.AssignedDepartmentId, t.AssignedToUserId, t.Status);

    private static string Describe(Ticket t) =>
        $"ticket(requester {t.RequesterId}/{t.RequesterDepartmentId}, handled {t.AssignedDepartmentId}/{t.AssignedToUserId})";
}
