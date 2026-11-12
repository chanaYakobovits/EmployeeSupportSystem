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

        var ticket = await ServiceFor(NoaId).GetAsync(id, CancellationToken.None);

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

        var error = await Assert.ThrowsAsync<NotFoundException>(() => ServiceFor(NoaId).GetAsync(idos, CancellationToken.None));

        Assert.Equal(TicketService.TicketNotFoundTitle, error.Title);
    }

    [Fact]
    public async Task A_handler_cannot_yet_view_a_ticket_assigned_to_them()
    {
        // Staff scopes come in task 3.4; until then only the requester sees a ticket.
        var id = AddTicket(NoaId, TicketStatus.Assigned, Now.AddHours(-1), configure: t => t.AssignedToUserId = LiatId);

        await Assert.ThrowsAsync<NotFoundException>(() => ServiceFor(LiatId).GetAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task A_ticket_of_another_organization_is_not_found_even_with_the_same_user_id()
    {
        // Same requester id as in organization B, but signed in to A: the organization filter hides it.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(OrgBUserId, organizationId: OrgA).GetAsync(OrgBTicketId, CancellationToken.None));

        // Control: its own organization's user does see it.
        var ticket = await ServiceFor(OrgBUserId, organizationId: OrgB).GetAsync(OrgBTicketId, CancellationToken.None);
        Assert.Equal(OrgBTicketId, ticket.Id);
    }

    [Fact]
    public async Task A_missing_ticket_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => ServiceFor(NoaId).GetAsync(987654, CancellationToken.None));
    }

    private static TicketStatusHistory History(TicketStatus? previous, TicketStatus next, int? by, DateTime at,
        string? reason = null) =>
        new() { PreviousStatus = previous, NewStatus = next, ChangedByUserId = by, ChangedAt = at, Reason = reason };
}
