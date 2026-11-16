using Common.DTOs.Tickets;
using Common.Enums;
using Common.Exceptions;
using Common.Models;
using Services.Implementations;

namespace Services.Tests.Tickets;

/// <summary>GET /api/tickets/{id} for the requester (spec 7.5); anything outside the scope is 404 (spec 2.4).</summary>
public sealed class TicketDetailsTests : TicketTestBase
{
    [Fact]
    public async Task The_requester_sees_details_and_a_chronological_status_timeline()
    {
        var created = Now.AddDays(-2);
        var id = AddTicket(NoaId, TicketStatus.InProgress, created, subject: "תלוש", description: "לא קיבלתי תלוש", configure: t =>
        {
            t.CategoryId = PayrollId;
            t.AssignedToUserId = LiatId;
            t.UpdatedAt = Now.AddHours(-1);
            // Added out of order on purpose.
            t.StatusHistory.Add(History(TicketStatus.Assigned, TicketStatus.InProgress, LiatId, Now.AddHours(-1), "הערת מטפל פנימית"));
            t.StatusHistory.Add(History(null, TicketStatus.New, NoaId, created));
            t.StatusHistory.Add(History(TicketStatus.New, TicketStatus.Assigned, null, created.AddMinutes(1)));
        });

        var ticket = Assert.IsType<RequesterTicketDetailsDto>(
            await ServiceFor(NoaId).GetAsync(id, ClientIp, CancellationToken.None));

        Assert.Equal(id, ticket.Id);
        Assert.Equal("תלוש", ticket.Subject);
        Assert.Equal("לא קיבלתי תלוש", ticket.Description);
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Equal("שכר ותלושים", ticket.CategoryName);
        Assert.Equal("ליאת שחר", ticket.HandlerName);
        Assert.Equal(created, ticket.CreatedAt);
        Assert.Equal(Now.AddHours(-1), ticket.UpdatedAt);
        Assert.Null(ticket.ResolvedAt);
        Assert.Null(ticket.ClosedAt);

        Assert.Equal(
            [(null, TicketStatus.New, "נועה ברקוביץ"), (TicketStatus.New, TicketStatus.Assigned, null),
                (TicketStatus.Assigned, TicketStatus.InProgress, "ליאת שחר")],
            ticket.Timeline.Select(h => (h.PreviousStatus, h.NewStatus, h.ChangedByName)));
    }

    [Fact]
    public async Task Another_employees_ticket_is_not_found()
    {
        var idos = AddTicket(IdoId, TicketStatus.New, Now.AddHours(-1));

        var error = await Assert.ThrowsAsync<NotFoundException>(() => ServiceFor(NoaId).GetAsync(idos, ClientIp, CancellationToken.None));

        Assert.Equal(TicketService.TicketNotFoundTitle, error.Title);
    }

    [Fact]
    public async Task A_handler_views_a_ticket_assigned_to_them_through_ticket_access()
    {
        // The details go through TicketAccess (staff scopes); staff get the staff DTO since task 3.4.
        var id = AddTicket(NoaId, TicketStatus.Assigned, Now.AddHours(-1), configure: t => t.AssignedToUserId = LiatId);

        var ticket = await ServiceFor(LiatId).GetAsync(id, ClientIp, CancellationToken.None);

        Assert.Equal(id, Assert.IsType<StaffTicketDetailsDto>(ticket).Id);
    }

    [Fact]
    public async Task A_handler_of_another_department_does_not_see_the_ticket()
    {
        var id = AddTicket(NoaId, TicketStatus.Assigned, Now.AddHours(-1), configure: t =>
        {
            t.AssignedDepartmentId = HrId;
            t.AssignedToUserId = LiatId;
        });

        await Assert.ThrowsAsync<NotFoundException>(() => ServiceFor(AlonId).GetAsync(id, ClientIp, CancellationToken.None));
    }

    [Fact]
    public async Task A_ticket_of_another_organization_is_not_found_even_for_an_executive()
    {
        // The executive sees every ticket of organization A – the organization filter still hides B's.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(AvnerId, organizationId: OrgA).GetAsync(OrgBTicketId, ClientIp, CancellationToken.None));

        // Control: its own organization's user does see it.
        var ticket = await ServiceFor(OrgBUserId, organizationId: OrgB).GetAsync(OrgBTicketId, ClientIp, CancellationToken.None);
        Assert.Equal(OrgBTicketId, ticket.Id);
    }

    [Fact]
    public async Task A_missing_ticket_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => ServiceFor(NoaId).GetAsync(987654, ClientIp, CancellationToken.None));
    }

    private static TicketStatusHistory History(TicketStatus? previous, TicketStatus next, int? by, DateTime at,
        string? reason = null) =>
        new() { PreviousStatus = previous, NewStatus = next, ChangedByUserId = by, ChangedAt = at, Reason = reason };
}
