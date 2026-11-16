using Common.DTOs.Routing;
using Common.Enums;
using Services.Implementations.Routing;

namespace Services.Tests.Routing;

/// <summary>The routing rules of spec 3.5 (task 3.3), pure: the data is given, no database.</summary>
public sealed class RoutingRulesTests
{
    private const int Development = 2;
    private const int Accounting = 3;
    private const int InformationSystems = 5;
    private const int CentralHr = 6;

    private const int Noa = 4;          // developer
    private const int DevHr = 10;       // departmental HR of Development (a handler in Development)
    private const int Alon = 11;        // accountants
    private const int Sigal = 12;
    private const int Amit = 13;
    private const int Shai = 15;        // IT technician
    private const int Yael = 23;        // central HR
    private const int Adi = 24;

    private static readonly List<HandlerCandidate> Organization =
    [
        new(DevHr, Development, 2),
        new(Alon, Accounting, 5),
        new(Sigal, Accounting, 3),
        new(Amit, Accounting, 3),
        new(Shai, InformationSystems, 7),
        new(Yael, CentralHr, 4),
        new(Adi, CentralHr, 1),
    ];

    private static RoutingDecision Fixed(int department, int requester, IReadOnlyList<HandlerCandidate>? candidates = null) =>
        RoutingRules.Decide(new RoutingInput(RoutingType.FixedDepartment, department, null, CentralHr, requester,
            candidates ?? Organization));

    private static RoutingDecision DepartmentHr(int? hrUserId, int requester, IReadOnlyList<HandlerCandidate>? candidates = null) =>
        RoutingRules.Decide(new RoutingInput(RoutingType.RequesterDepartmentHr, null, hrUserId, CentralHr, requester,
            candidates ?? Organization));

    // ---------- category with a fixed department ----------

    [Fact]
    public void A_fixed_category_goes_to_its_handling_department()
    {
        var decision = Fixed(InformationSystems, Noa);

        Assert.Equal(new RoutingDecision(InformationSystems, Shai, RoutingReason.FixedDepartment), decision);
    }

    [Fact]
    public void Several_handlers_the_one_with_the_fewest_open_tickets_gets_it()
    {
        // Central HR: Yael has 4, Adi 1.
        Assert.Equal(Adi, Fixed(CentralHr, Noa).HandlerUserId);
    }

    [Fact]
    public void A_tie_goes_to_the_lowest_id()
    {
        // Accounting: Alon 5, Sigal 3, Amit 3 → Sigal (12) before Amit (13).
        Assert.Equal(Sigal, Fixed(Accounting, Noa).HandlerUserId);
    }

    [Fact]
    public void The_requester_is_never_chosen_even_with_the_lowest_load()
    {
        // Sigal opens a payroll ticket: Amit has the same load and is the next one.
        Assert.Equal(Amit, Fixed(Accounting, Sigal).HandlerUserId);
    }

    [Fact]
    public void A_handler_who_is_not_a_candidate_is_never_chosen()
    {
        // An inactive handler (or one without the Handler role) is not returned by the query, so not in the list.
        var withoutShai = Organization.Where(c => c.UserId != Shai).ToList();

        var decision = Fixed(InformationSystems, Noa, withoutShai);

        Assert.Equal(new RoutingDecision(CentralHr, Adi, RoutingReason.NoHandlerInDepartment), decision);
    }

    [Fact]
    public void The_requester_as_the_only_handler_of_the_department_goes_to_central_hr()
    {
        // Shai's own equipment ticket: IT has no one else.
        var decision = Fixed(InformationSystems, Shai);

        Assert.Equal(new RoutingDecision(CentralHr, Adi, RoutingReason.NoHandlerInDepartment), decision);
    }

    [Fact]
    public void No_handler_anywhere_goes_to_the_central_hr_queue_without_a_handler()
    {
        var decision = Fixed(InformationSystems, Noa, candidates: []);

        Assert.Equal(new RoutingDecision(CentralHr, null, RoutingReason.NoHandlerInDepartment), decision);
    }

    [Fact]
    public void The_fallback_category_without_handlers_stays_in_the_central_hr_queue()
    {
        var decision = Fixed(CentralHr, Noa, candidates: []);

        Assert.Equal(new RoutingDecision(CentralHr, null, RoutingReason.FixedDepartment), decision);
    }

    // ---------- category routed to the requester's departmental HR ----------

    [Fact]
    public void A_departmental_hr_category_goes_to_the_hr_handler_of_the_requesters_department()
    {
        var decision = DepartmentHr(DevHr, Noa);

        Assert.Equal(new RoutingDecision(Development, DevHr, RoutingReason.DepartmentHr), decision);
    }

    [Fact]
    public void The_hr_handler_may_belong_to_another_department_and_the_ticket_goes_to_their_department()
    {
        // Adi of central HR serves Development as its HR handler.
        var decision = DepartmentHr(Adi, Noa);

        Assert.Equal(new RoutingDecision(CentralHr, Adi, RoutingReason.DepartmentHr), decision);
    }

    [Fact]
    public void A_department_without_hr_goes_to_central_hr()
    {
        var decision = DepartmentHr(hrUserId: null, Noa);

        Assert.Equal(new RoutingDecision(CentralHr, Adi, RoutingReason.DepartmentHrMissing), decision);
    }

    [Fact]
    public void A_deactivated_hr_handler_goes_to_central_hr()
    {
        // Still set on the department, but not returned as a candidate (inactive or no Handler role).
        var withoutDevHr = Organization.Where(c => c.UserId != DevHr).ToList();

        var decision = DepartmentHr(DevHr, Noa, withoutDevHr);

        Assert.Equal(new RoutingDecision(CentralHr, Adi, RoutingReason.DepartmentHrUnavailable), decision);
    }

    [Fact]
    public void The_hr_handler_opening_their_own_attendance_ticket_goes_to_central_hr()
    {
        var decision = DepartmentHr(DevHr, DevHr);

        Assert.Equal(new RoutingDecision(CentralHr, Adi, RoutingReason.DepartmentHrUnavailable), decision);
    }

    [Fact]
    public void An_accountants_attendance_ticket_never_reaches_the_other_accountants()
    {
        // Accounting has handlers, but no departmental HR: the ticket goes to central HR, not to Sigal or Amit.
        var decision = DepartmentHr(hrUserId: null, Alon);

        Assert.Equal(CentralHr, decision.DepartmentId);
        Assert.DoesNotContain(decision.HandlerUserId, new int?[] { Alon, Sigal, Amit });
        Assert.Equal(RoutingReason.DepartmentHrMissing, decision.Reason);
    }

    [Fact]
    public void Central_hr_without_handlers_leaves_a_departmental_hr_ticket_in_its_queue()
    {
        var decision = DepartmentHr(hrUserId: null, Noa, candidates: []);

        Assert.Equal(new RoutingDecision(CentralHr, null, RoutingReason.DepartmentHrMissing), decision);
    }
}
