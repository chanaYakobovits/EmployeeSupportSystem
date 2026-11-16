using System.Text.Json;
using Common.Constants;
using Common.DTOs.Tickets;
using Common.Enums;
using Common.Exceptions;

namespace Services.Tests.Tickets;

/// <summary>GET /api/tickets/{id} for staff (spec 7.7), and the audit record of the view (spec 6.4).</summary>
public sealed class StaffTicketDetailsTests : TicketTestBase
{
    private readonly int _ticketId;

    public StaffTicketDetailsTests()
    {
        _ticketId = AddTicket(NoaId, TicketStatus.InProgress, Now.AddDays(-1), subject: "תלוש", description: "לא קיבלתי תלוש",
            configure: t =>
            {
                t.CategoryId = PayrollId;
                t.Urgency = null;
                t.ClassificationSource = ClassificationSource.Fallback;
                t.AssignedDepartmentId = DevId;
                t.AssignedToUserId = AlonId;
                t.DueAt = Now.AddMinutes(-5);
                t.UpdatedAt = Now.AddHours(-1);
                t.RowVersion = [1, 2, 3];
            });
    }

    [Fact]
    public async Task A_handler_gets_the_staff_view_with_the_requester_sla_and_routing()
    {
        var ticket = Assert.IsType<StaffTicketDetailsDto>(await Get(AlonId));

        Assert.Equal(_ticketId, ticket.Id);
        Assert.Equal("תלוש", ticket.Subject);
        Assert.Equal("לא קיבלתי תלוש", ticket.Description);
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Equal(new StaffTicketRequesterDto(NoaId, "נועה ברקוביץ", "פיתוח", "J"), ticket.Requester);
        Assert.Equal(new NamedRefDto(PayrollId, "שכר ותלושים"), ticket.Category);
        Assert.Null(ticket.Urgency);
        Assert.Equal(ClassificationSource.Fallback, ticket.ClassificationSource);
        Assert.Equal(Now.AddMinutes(-5), ticket.DueAt);
        Assert.True(ticket.IsOverdue);
        Assert.Equal(new NamedRefDto(AlonId, "אלון ביטון"), ticket.AssignedTo);
        Assert.Equal(new NamedRefDto(DevId, "פיתוח"), ticket.AssignedDepartment);
        Assert.Equal(Now.AddDays(-1), ticket.CreatedAt);
        Assert.Equal(Now.AddHours(-1), ticket.UpdatedAt);
        Assert.Equal([1, 2, 3], ticket.RowVersion);
        Assert.Equal(new StaffTicketPermissionsDto(CanReply: true, CanWriteInternal: true, CanReclassify: true, CanAssign: true,
            CanTransferToDepartment: true), ticket.Permissions);
    }

    [Fact]
    public async Task A_department_manager_and_the_executive_get_the_staff_view_with_their_permissions()
    {
        var ron = Assert.IsType<StaffTicketDetailsDto>(await Get(RonId));
        var avner = Assert.IsType<StaffTicketDetailsDto>(await Get(AvnerId));

        // Ron manages פיתוח, which handles the ticket: reclassify and assign within it, not to another department.
        Assert.Equal(new StaffTicketPermissionsDto(CanReply: false, CanWriteInternal: true, CanReclassify: true,
            CanAssign: true, CanTransferToDepartment: false), ron.Permissions);
        Assert.Equal(new StaffTicketPermissionsDto(false, false, false, false, false), avner.Permissions);
    }

    [Fact]
    public async Task A_ticket_waiting_for_the_employee_is_not_overdue()
    {
        var id = AddTicket(NoaId, TicketStatus.WaitingForEmployee, Now.AddDays(-1), configure: t =>
        {
            t.AssignedDepartmentId = DevId;
            t.DueAt = Now.AddHours(-3);
        });

        var ticket = Assert.IsType<StaffTicketDetailsDto>(await ServiceFor(AlonId).GetAsync(id, ClientIp, CancellationToken.None));

        Assert.False(ticket.IsOverdue);
    }

    [Fact]
    public async Task Staff_who_opened_the_ticket_get_the_requester_view_and_no_audit_record()
    {
        var alons = AddTicket(AlonId, TicketStatus.Assigned, Now.AddHours(-1), configure: t => t.AssignedDepartmentId = DevId);

        Assert.IsType<RequesterTicketDetailsDto>(await ServiceFor(AlonId).GetAsync(alons, ClientIp, CancellationToken.None));
        Assert.IsType<RequesterTicketDetailsDto>(await Get(NoaId));

        Assert.Empty(AllAuditLogs());
    }

    [Fact]
    public async Task Every_staff_view_is_recorded_with_the_viewer_ticket_and_address()
    {
        await Get(AlonId);
        await Get(AlonId);
        await Get(AvnerId);

        var records = AllAuditLogs();
        Assert.Equal(3, records.Count);
        Assert.All(records, r =>
        {
            Assert.Equal(AuditActions.TicketViewed, r.Action);
            Assert.Equal(AuditEntityTypes.Ticket, r.EntityType);
            Assert.Equal(_ticketId, r.EntityId);
            Assert.Equal(OrgA, r.OrganizationId);
            Assert.Equal(ClientIp, r.IpAddress);
            Assert.Equal(Now, r.CreatedAt);
        });
        Assert.Equal([AlonId, AlonId, AvnerId], records.Select(r => r.UserId));
    }

    [Fact]
    public async Task Outside_every_scope_is_not_found_and_not_recorded()
    {
        var hrTicket = AddTicket(LiatId, TicketStatus.Assigned, Now.AddHours(-1), configure: t =>
        {
            t.RequesterDepartmentId = HrId;
            t.AssignedDepartmentId = HrId;
        });

        await Assert.ThrowsAsync<NotFoundException>(() => ServiceFor(AlonId).GetAsync(hrTicket, ClientIp, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => ServiceFor(RonId).GetAsync(hrTicket, ClientIp, CancellationToken.None));

        Assert.Empty(AllAuditLogs());
    }

    [Fact]
    public async Task The_json_is_the_staff_dto_with_the_sla_fields()
    {
        var json = JsonSerializer.Serialize<object>(await Get(AlonId), ApiJson);

        Assert.Contains("\"isOverdue\":true", json);
        Assert.Contains("\"classificationSource\":\"Fallback\"", json);
        Assert.Contains("\"rowVersion\":\"AQID\"", json);
        Assert.Contains("\"jobTitle\":\"J\"", json);
        Assert.DoesNotContain("timeline", json);
    }

    private Task<ITicketDetailsDto> Get(int userId) => ServiceFor(userId).GetAsync(_ticketId, ClientIp, CancellationToken.None);
}
