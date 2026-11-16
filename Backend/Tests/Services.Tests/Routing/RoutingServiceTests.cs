using Common.DTOs.Routing;
using Common.Enums;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Services.Tests.Tickets;

namespace Services.Tests.Routing;

/// <summary>
/// RoutingService and TicketClassifier over EF Core InMemory (task 3.3): the data comes from the database and the
/// cache – activity, roles and load of the handlers, HrUserId, the fallback, the SLA policies.
/// On top of <see cref="TicketTestBase"/>: Accounting (handlers Sigal and Amit), a second HR handler Dana, category
/// ATTENDANCE (departmental HR), Alon as the departmental HR of Development, SLA High = 10 hours.
/// </summary>
public sealed class RoutingServiceTests : TicketTestBase
{
    private const int AccountingId = 12;
    private const int AttendanceId = 3;
    private const int SigalId = 30;   // accountant (handler)
    private const int AmitId = 31;    // accountant (handler)
    private const int DanaId = 32;    // second handler in HR (central)

    public RoutingServiceTests()
    {
        using var db = CreateContext(signedInUserId: null);
        db.Departments.Add(new Department { Id = AccountingId, OrganizationId = OrgA, Name = "הנהלת חשבונות", IsActive = true });
        db.Users.AddRange(
            Handler(SigalId, AccountingId),
            Handler(AmitId, AccountingId),
            Handler(DanaId, HrId));
        db.Categories.Add(new Category
        {
            Id = AttendanceId, OrganizationId = OrgA, Code = "ATTENDANCE", Name = "נוכחות", IsActive = true,
            RoutingType = RoutingType.RequesterDepartmentHr,
        });
        db.SlaPolicies.Add(new SlaPolicy { OrganizationId = OrgA, Urgency = Urgency.High, ResolutionHours = 10 });
        db.SaveChanges();

        db.Departments.IgnoreQueryFilters().Single(d => d.Id == DevId).HrUserId = AlonId;
        db.SaveChanges();
    }

    private Task<RoutingDecision> RouteAsync(int categoryId, int requesterId, int requesterDepartmentId)
    {
        var db = CreateContext(requesterId);
        return RoutingFor(db).RouteAsync(OrgA, categoryId, requesterId, requesterDepartmentId, CancellationToken.None);
    }

    [Fact]
    public async Task Departmental_hr_category_goes_to_the_hr_handler_set_on_the_requesters_department()
    {
        var decision = await RouteAsync(AttendanceId, NoaId, DevId);

        Assert.Equal(new RoutingDecision(DevId, AlonId, RoutingReason.DepartmentHr), decision);
    }

    [Fact]
    public async Task A_deactivated_departmental_hr_goes_to_central_hr()
    {
        SetUser(AlonId, u => u.IsActive = false);

        var decision = await RouteAsync(AttendanceId, NoaId, DevId);

        Assert.Equal(HrId, decision.DepartmentId);
        Assert.Equal(RoutingReason.DepartmentHrUnavailable, decision.Reason);
    }

    [Fact]
    public async Task A_departmental_hr_without_the_handler_role_goes_to_central_hr()
    {
        SetUser(AlonId, u => u.Roles.Remove(u.Roles.Single(r => r.Role == Role.Handler)));

        var decision = await RouteAsync(AttendanceId, NoaId, DevId);

        Assert.Equal(RoutingReason.DepartmentHrUnavailable, decision.Reason);
    }

    [Fact]
    public async Task An_accountants_attendance_ticket_goes_to_central_hr_not_to_the_accountants()
    {
        var decision = await RouteAsync(AttendanceId, SigalId, AccountingId);

        Assert.Equal(HrId, decision.DepartmentId);
        Assert.Contains(decision.HandlerUserId, new int?[] { LiatId, DanaId });
        Assert.Equal(RoutingReason.DepartmentHrMissing, decision.Reason);
    }

    [Fact]
    public async Task Load_counts_unresolved_tickets_including_waiting_but_not_resolved()
    {
        // Liat: one WaitingForEmployee (counts). Dana: two Resolved and one Closed (do not count).
        Assign(LiatId, TicketStatus.WaitingForEmployee);
        Assign(DanaId, TicketStatus.Resolved);
        Assign(DanaId, TicketStatus.Resolved);
        Assign(DanaId, TicketStatus.Closed);

        var decision = await RouteAsync(GeneralId, NoaId, DevId);

        Assert.Equal(DanaId, decision.HandlerUserId);
    }

    [Fact]
    public async Task Equal_load_goes_to_the_lowest_id()
    {
        var decision = await RouteAsync(GeneralId, NoaId, DevId);

        Assert.Equal(LiatId, decision.HandlerUserId); // 3 < 32
    }

    [Fact]
    public async Task An_inactive_handler_is_never_chosen()
    {
        SetUser(LiatId, u => u.IsActive = false);

        var decision = await RouteAsync(GeneralId, NoaId, DevId);

        Assert.Equal(DanaId, decision.HandlerUserId);
    }

    [Fact]
    public async Task No_handler_at_all_leaves_the_ticket_in_the_central_hr_queue()
    {
        SetUser(LiatId, u => u.IsActive = false);
        SetUser(DanaId, u => u.IsActive = false);

        var decision = await RouteAsync(GeneralId, NoaId, DevId);

        Assert.Equal(new RoutingDecision(HrId, null, RoutingReason.FixedDepartment), decision);
    }

    [Fact]
    public async Task Central_hr_is_the_fallbacks_department_not_a_department_by_name()
    {
        // Move GENERAL to Accounting: "central HR" follows it.
        using (var db = CreateContext(signedInUserId: null))
        {
            db.Categories.IgnoreQueryFilters().Single(c => c.Id == GeneralId).HandlingDepartmentId = AccountingId;
            db.SaveChanges();
        }

        var decision = await RouteAsync(AttendanceId, SigalId, AccountingId);

        Assert.Equal(new RoutingDecision(AccountingId, AmitId, RoutingReason.DepartmentHrMissing), decision);
    }

    // ===================== TicketClassifier – the entry point for 3.4 / 4.3 =====================

    [Fact]
    public async Task A_new_category_routes_again_and_recalculates_the_due_date_from_the_opening()
    {
        var created = Now.AddHours(-5);
        var id = AddTicket(NoaId, TicketStatus.InProgress, created, configure: t =>
        {
            t.CategoryId = GeneralId;
            t.ClassificationSource = ClassificationSource.Fallback;
            t.AssignedDepartmentId = HrId;
            t.AssignedToUserId = LiatId;
            t.DueAt = created.AddHours(72);
        });

        var (ticket, decision) = await ApplyAsync(id, new TicketClassification(PayrollId, Urgency.High, ClassificationSource.Manual));

        Assert.NotNull(decision);
        Assert.Equal(PayrollId, ticket.CategoryId);
        Assert.Equal(Urgency.High, ticket.Urgency);
        Assert.Equal(ClassificationSource.Manual, ticket.ClassificationSource);
        Assert.Equal(DevId, ticket.AssignedDepartmentId); // PAYROLL → Development (Alon)
        Assert.Equal(AlonId, ticket.AssignedToUserId);
        Assert.Equal(created.AddHours(10), ticket.DueAt); // High = 10 in this organization
        Assert.Equal(TicketStatus.InProgress, ticket.Status); // what a transfer does to the status is task 3.4
        Assert.Equal(Now, ticket.UpdatedAt);
        Assert.Empty(ticket.StatusHistory);
    }

    [Fact]
    public async Task Only_the_urgency_changed_keeps_the_assignment_and_moves_the_due_date()
    {
        var created = Now.AddHours(-1);
        var id = AddTicket(NoaId, TicketStatus.Assigned, created, configure: t =>
        {
            t.CategoryId = GeneralId;
            t.ClassificationSource = ClassificationSource.Fallback;
            t.AssignedDepartmentId = HrId;
            t.AssignedToUserId = DanaId; // not who the routing would choose now
            t.DueAt = created.AddHours(72);
        });

        var (ticket, decision) = await ApplyAsync(id, new TicketClassification(GeneralId, Urgency.Critical, ClassificationSource.Manual));

        Assert.Null(decision);
        Assert.Equal(DanaId, ticket.AssignedToUserId);
        Assert.Equal(created.AddHours(4), ticket.DueAt); // Critical: default 4 hours (no policy row)
    }

    [Fact]
    public async Task A_new_ticket_becomes_assigned_with_a_system_history_row()
    {
        var id = AddTicket(NoaId, TicketStatus.New, Now);

        var (ticket, _) = await ApplyAsync(id, new TicketClassification(GeneralId, null, ClassificationSource.Ai));

        Assert.Equal(TicketStatus.Assigned, ticket.Status);
        var row = Assert.Single(ticket.StatusHistory);
        Assert.Equal(TicketStatus.New, row.PreviousStatus);
        Assert.Equal(TicketStatus.Assigned, row.NewStatus);
        Assert.Null(row.ChangedByUserId);
        Assert.Equal(Now.AddHours(72), ticket.DueAt);
    }

    private async Task<(Ticket Ticket, RoutingDecision? Decision)> ApplyAsync(int ticketId, TicketClassification classification)
    {
        using var db = CreateContext(AlonId);
        var ticket = await db.Tickets.SingleAsync(t => t.Id == ticketId);
        var decision = await ClassifierFor(db, RoutingFor(db)).ApplyAsync(ticket, classification, Now, CancellationToken.None);
        await db.SaveChangesAsync();
        return (AllTickets().Single(t => t.Id == ticketId), decision);
    }

    private void Assign(int handlerId, TicketStatus status) =>
        AddTicket(IdoId, status, Now.AddDays(-2), configure: t =>
        {
            t.AssignedDepartmentId = HrId;
            t.AssignedToUserId = handlerId;
        });

    private void SetUser(int userId, Action<User> change)
    {
        using var db = CreateContext(signedInUserId: null);
        var user = db.Users.IgnoreQueryFilters().Include(u => u.Roles).Single(u => u.Id == userId);
        change(user);
        db.SaveChanges();
    }

    private static User Handler(int id, int departmentId) => new()
    {
        Id = id,
        OrganizationId = OrgA,
        DepartmentId = departmentId,
        EmployeeCode = $"EMP-{id:D5}",
        FirstName = "מטפל",
        LastName = id.ToString(),
        Email = $"user{id}@example.com",
        JobTitle = "J",
        PasswordHash = "h",
        IsActive = true,
        CreatedAt = Now.AddDays(-100),
        Roles = [new UserRole { UserId = id, Role = Role.Employee }, new UserRole { UserId = id, Role = Role.Handler }],
    };
}
