using Common.DTOs.Tickets;
using Common.Enums;

namespace Services.Tests.Tickets;

/// <summary>GET /api/tickets/my and /my/summary (spec 7.3, 9.4) as real queries.</summary>
public sealed class MyTicketsTests : TicketTestBase
{
    [Fact]
    public async Task Only_the_users_own_tickets_are_listed()
    {
        var mine = AddTicket(NoaId, TicketStatus.New, Now.AddHours(-2));
        AddTicket(IdoId, TicketStatus.New, Now.AddHours(-1));

        var page = await ServiceFor(NoaId).GetMyPageAsync(new MyTicketsQuery(), CancellationToken.None);

        Assert.Equal([mine], page.Items.Select(t => t.Id));
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task Pages_have_the_right_size_and_the_newest_tickets_come_first()
    {
        var ids = Enumerable.Range(1, 30).Select(i => AddTicket(NoaId, TicketStatus.New, Now.AddHours(-i))).ToList();
        var service = ServiceFor(NoaId);

        var first = await service.GetMyPageAsync(new MyTicketsQuery { PageSize = 25 }, CancellationToken.None);
        var second = await service.GetMyPageAsync(new MyTicketsQuery { PageSize = 25, Page = 2 }, CancellationToken.None);
        var beyond = await service.GetMyPageAsync(new MyTicketsQuery { PageSize = 25, Page = 5 }, CancellationToken.None);

        Assert.Equal(30, first.TotalCount);
        Assert.Equal(ids.Take(25), first.Items.Select(t => t.Id));   // created most recently first
        Assert.Equal(ids.Skip(25), second.Items.Select(t => t.Id));
        Assert.Empty(beyond.Items);
        Assert.Equal(30, beyond.TotalCount);
    }

    [Fact]
    public async Task Several_statuses_can_be_filtered_at_once()
    {
        var newId = AddTicket(NoaId, TicketStatus.New, Now.AddHours(-1));
        var assigned = AddTicket(NoaId, TicketStatus.Assigned, Now.AddHours(-2));
        AddTicket(NoaId, TicketStatus.InProgress, Now.AddHours(-3));
        AddTicket(NoaId, TicketStatus.Closed, Now.AddHours(-4));

        var page = await ServiceFor(NoaId).GetMyPageAsync(
            new MyTicketsQuery { Status = [TicketStatus.New, TicketStatus.Assigned] }, CancellationToken.None);

        Assert.Equal([newId, assigned], page.Items.Select(t => t.Id));
        Assert.Equal(2, page.TotalCount);
    }

    [Fact]
    public async Task Search_matches_the_subject()
    {
        var payslip = AddTicket(NoaId, TicketStatus.New, Now.AddHours(-1), subject: "טעות בתלוש נובמבר");
        AddTicket(NoaId, TicketStatus.New, Now.AddHours(-2), subject: "מחשב איטי");
        AddTicket(NoaId, TicketStatus.New, Now.AddHours(-3), subject: null, description: "שאלה על התלוש שלי");

        var page = await ServiceFor(NoaId).GetMyPageAsync(new MyTicketsQuery { Search = " תלוש " }, CancellationToken.None);

        Assert.Equal([payslip], page.Items.Select(t => t.Id));
    }

    [Theory]
    [InlineData("updatedAt", null, new[] { 1, 2, 0 })]       // non-default column: ascending
    [InlineData("updatedAt", "desc", new[] { 0, 2, 1 })]
    [InlineData("status", null, new[] { 2, 0, 1 })]          // workflow order: New, InProgress, Closed
    [InlineData("createdAt", "asc", new[] { 2, 1, 0 })]
    public async Task Sorting_by_each_allowed_field(string sortBy, string? sortDir, int[] expectedOrder)
    {
        int[] ids =
        [
            AddTicket(NoaId, TicketStatus.InProgress, Now.AddHours(-1), configure: t => t.UpdatedAt = Now.AddMinutes(-1)),
            AddTicket(NoaId, TicketStatus.Closed, Now.AddHours(-2), configure: t => t.UpdatedAt = Now.AddHours(-50)),
            AddTicket(NoaId, TicketStatus.New, Now.AddHours(-3), configure: t => t.UpdatedAt = Now.AddHours(-3)),
        ];

        var page = await ServiceFor(NoaId).GetMyPageAsync(new MyTicketsQuery { SortBy = sortBy, SortDir = sortDir },
            CancellationToken.None);

        Assert.Equal(expectedOrder.Select(i => ids[i]), page.Items.Select(t => t.Id));
    }

    [Fact]
    public async Task A_row_shows_category_handler_and_dates()
    {
        var id = AddTicket(NoaId, TicketStatus.Assigned, Now.AddHours(-5), configure: t =>
        {
            t.CategoryId = PayrollId;
            t.AssignedToUserId = LiatId;
            t.UpdatedAt = Now.AddHours(-1);
        });
        var unclassified = AddTicket(NoaId, TicketStatus.New, Now.AddHours(-6));

        var items = (await ServiceFor(NoaId).GetMyPageAsync(new MyTicketsQuery(), CancellationToken.None)).Items;

        var row = items.Single(t => t.Id == id);
        Assert.Equal("נושא", row.Subject);
        Assert.False(row.IsSubjectExcerpt);
        Assert.Equal("שכר ותלושים", row.CategoryName);
        Assert.Equal(TicketStatus.Assigned, row.Status);
        Assert.Equal("ליאת שחר", row.HandlerName);
        Assert.Equal(Now.AddHours(-5), row.CreatedAt);
        Assert.Equal(Now.AddHours(-1), row.UpdatedAt);

        var pending = items.Single(t => t.Id == unclassified);
        Assert.Null(pending.CategoryName);
        Assert.Null(pending.HandlerName);
    }

    [Fact]
    public async Task Without_a_subject_the_row_shows_the_first_80_characters_of_the_description()
    {
        var longText = string.Concat(Enumerable.Repeat("אבגדהוזחטי", 10));   // 100 characters
        var longId = AddTicket(NoaId, TicketStatus.New, Now.AddHours(-1), subject: null, description: longText);
        var shortId = AddTicket(NoaId, TicketStatus.New, Now.AddHours(-2), subject: null, description: "תיאור קצר מאוד");

        var items = (await ServiceFor(NoaId).GetMyPageAsync(new MyTicketsQuery(), CancellationToken.None)).Items;

        var longRow = items.Single(t => t.Id == longId);
        Assert.Equal(longText[..80], longRow.Subject);
        Assert.True(longRow.IsSubjectExcerpt);
        Assert.Equal("תיאור קצר מאוד", items.Single(t => t.Id == shortId).Subject);
    }

    // ===================== Summary =====================

    [Fact]
    public async Task Summary_counts_open_waiting_and_closed_this_month()
    {
        // Open: New, Assigned, InProgress, Resolved (not yet confirmed).
        AddTicket(NoaId, TicketStatus.New, Now.AddDays(-1));
        AddTicket(NoaId, TicketStatus.Assigned, Now.AddDays(-2));
        AddTicket(NoaId, TicketStatus.InProgress, Now.AddDays(-3));
        AddTicket(NoaId, TicketStatus.Resolved, Now.AddDays(-4));
        // Waiting for me.
        AddTicket(NoaId, TicketStatus.WaitingForEmployee, Now.AddDays(-5));
        AddTicket(NoaId, TicketStatus.WaitingForEmployee, Now.AddDays(-6));
        // Closed this month (Israel time): 1 Nov 01:00 Israel = 31 Oct 23:00 UTC counts; 31 Oct 23:00 Israel does not.
        AddTicket(NoaId, TicketStatus.Closed, Now.AddDays(-20), configure: t => t.ClosedAt = new DateTime(2026, 10, 31, 23, 0, 0, DateTimeKind.Utc));
        AddTicket(NoaId, TicketStatus.Closed, Now.AddDays(-20), configure: t => t.ClosedAt = Now.AddHours(-1));
        AddTicket(NoaId, TicketStatus.Closed, Now.AddDays(-30), configure: t => t.ClosedAt = new DateTime(2026, 10, 31, 21, 0, 0, DateTimeKind.Utc));
        // Not counted anywhere.
        AddTicket(NoaId, TicketStatus.Cancelled, Now.AddDays(-1));
        AddTicket(IdoId, TicketStatus.New, Now.AddDays(-1));
        AddTicket(IdoId, TicketStatus.WaitingForEmployee, Now.AddDays(-1));
        AddTicket(IdoId, TicketStatus.Closed, Now.AddDays(-1), configure: t => t.ClosedAt = Now.AddHours(-1));

        var summary = await ServiceFor(NoaId).GetMySummaryAsync(CancellationToken.None);

        Assert.Equal(new RequesterTicketsSummaryDto(Open: 4, WaitingForMe: 2, ClosedThisMonth: 2), summary);
    }

    [Fact]
    public async Task Summary_of_a_user_without_tickets_is_all_zeros()
    {
        AddTicket(IdoId, TicketStatus.New, Now.AddDays(-1));

        var summary = await ServiceFor(NoaId).GetMySummaryAsync(CancellationToken.None);

        Assert.Equal(new RequesterTicketsSummaryDto(0, 0, 0), summary);
    }
}
