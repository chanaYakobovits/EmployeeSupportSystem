using Common.Enums;
using Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using Repositories.Interfaces;

namespace Services.Tests.Tickets;

/// <summary>
/// Task 3.5, spec 10.2, 14.9: resolved tickets close 7 days after the resolution – exactly then, not before; a ticket
/// the employee changed meanwhile is skipped quietly; every organization's tickets, each closed in its own scope.
/// </summary>
public sealed class AutoCloseServiceTests : TicketTestBase
{
    [Fact]
    public async Task A_ticket_resolved_exactly_7_days_ago_is_closed_by_the_system()
    {
        var id = Resolved(Now.AddDays(-7));

        var due = await AutoCloseFor(null).GetDueAsync(100, CancellationToken.None);
        var closed = await AutoCloseFor(OrgA).TryCloseAsync(id, CancellationToken.None);

        Assert.Contains(due, d => d.TicketId == id && d.OrganizationId == OrgA);
        Assert.True(closed);
        var ticket = AllTickets().Single(t => t.Id == id);
        Assert.Equal((TicketStatus.Closed, (DateTime?)Now), (ticket.Status, ticket.ClosedAt));
        var row = Assert.Single(ticket.StatusHistory);
        Assert.Equal((TicketStatus.Resolved, TicketStatus.Closed, (int?)null),
            (row.PreviousStatus!.Value, row.NewStatus, row.ChangedByUserId));
    }

    [Fact]
    public async Task A_minute_before_7_days_it_stays_resolved()
    {
        var id = Resolved(Now.AddDays(-7).AddMinutes(1));

        var due = await AutoCloseFor(null).GetDueAsync(100, CancellationToken.None);
        var closed = await AutoCloseFor(OrgA).TryCloseAsync(id, CancellationToken.None);

        Assert.DoesNotContain(due, d => d.TicketId == id);
        Assert.False(closed);
        Assert.Equal(TicketStatus.Resolved, AllTickets().Single(t => t.Id == id).Status);
    }

    [Fact]
    public async Task A_ticket_reopened_since_it_was_listed_is_skipped()
    {
        var id = AddTicket(NoaId, TicketStatus.InProgress, Now.AddDays(-20), configure: t =>
            t.ResolvedAt = Now.AddDays(-8));

        Assert.False(await AutoCloseFor(OrgA).TryCloseAsync(id, CancellationToken.None));
        Assert.Equal(TicketStatus.InProgress, AllTickets().Single(t => t.Id == id).Status);
    }

    [Fact]
    public async Task A_conflict_with_the_employees_action_is_skipped_quietly()
    {
        var id = Resolved(Now.AddDays(-8));
        UnitOfWorkFactory = _ => new ConflictingUnitOfWork();

        var closed = await AutoCloseFor(OrgA).TryCloseAsync(id, CancellationToken.None);

        UnitOfWorkFactory = null;
        Assert.False(closed);
        Assert.Equal(TicketStatus.Resolved, AllTickets().Single(t => t.Id == id).Status);
        Assert.Contains("skipped", AutoCloseLogger.AllText);
    }

    [Fact]
    public async Task Every_organization_is_listed_and_each_ticket_is_closed_only_in_its_own_scope()
    {
        using (var db = CreateContext(signedInUserId: null))
        {
            var other = db.Tickets.IgnoreQueryFilters().Single(t => t.Id == OrgBTicketId);
            other.Status = TicketStatus.Resolved;
            other.ResolvedAt = Now.AddDays(-10);
            db.SaveChanges();
        }

        var due = await AutoCloseFor(null).GetDueAsync(100, CancellationToken.None);

        Assert.Contains(due, d => d.TicketId == OrgBTicketId && d.OrganizationId == OrgB);
        // Organization A's scope does not see it.
        Assert.False(await AutoCloseFor(OrgA).TryCloseAsync(OrgBTicketId, CancellationToken.None));
        Assert.True(await AutoCloseFor(OrgB).TryCloseAsync(OrgBTicketId, CancellationToken.None));
    }

    [Fact]
    public async Task The_oldest_come_first_up_to_the_batch_size()
    {
        var newer = Resolved(Now.AddDays(-8));
        var older = Resolved(Now.AddDays(-9));

        var due = await AutoCloseFor(null).GetDueAsync(1, CancellationToken.None);

        Assert.Equal(older, Assert.Single(due).TicketId);
        Assert.NotEqual(newer, older);
    }

    private int Resolved(DateTime resolvedAt) =>
        AddTicket(NoaId, TicketStatus.Resolved, resolvedAt.AddDays(-1), configure: t =>
        {
            t.ResolvedAt = resolvedAt;
            t.AssignedDepartmentId = DevId;
            t.AssignedToUserId = AlonId;
        });

    private sealed class ConflictingUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            Task.FromException(new ConflictException(errorType: "concurrency"));

        public void DiscardChanges() { }

        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
