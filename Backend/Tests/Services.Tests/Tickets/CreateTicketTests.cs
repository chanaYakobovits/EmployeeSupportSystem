using System.Text.Json;
using Common.DTOs.Tickets;
using Common.Enums;
using Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using Repositories.Implementations;
using Repositories.Interfaces;

namespace Services.Tests.Tickets;

/// <summary>POST /api/tickets (spec 3.1, 4.3): requester from the token, department snapshot, initial state.</summary>
public sealed class CreateTicketTests : TicketTestBase
{
    private const string Description = "לא קיבלתי את תלוש השכר של החודש";

    [Fact]
    public async Task The_requester_comes_from_the_token_even_when_the_body_names_another_user()
    {
        // As the API binds it: unknown properties ("requesterId", "requesterDepartmentId") are ignored.
        var json = $$"""{ "requesterId": {{IdoId}}, "requesterDepartmentId": {{HrId}}, "subject": "תלוש", "description": "{{Description}}" }""";
        var request = JsonSerializer.Deserialize<CreateTicketRequest>(json, ApiJson)!;

        var created = await ServiceFor(NoaId).CreateAsync(request, [], CancellationToken.None);

        var ticket = AllTickets().Single(t => t.Id == created.Id);
        Assert.Equal(NoaId, ticket.RequesterId);
        Assert.Equal(DevId, ticket.RequesterDepartmentId);
        Assert.Equal(OrgA, ticket.OrganizationId);
    }

    [Fact]
    public async Task The_department_is_read_from_the_database_and_kept_as_a_snapshot()
    {
        // The token's department claim (1) is stale; the database says Development.
        var created = await ServiceFor(NoaId).CreateAsync(Request(), [], CancellationToken.None);
        Assert.NotEqual(ClaimDepartmentId, DevId);
        Assert.Equal(DevId, AllTickets().Single(t => t.Id == created.Id).RequesterDepartmentId);

        // Noa moves to HR: her existing ticket keeps Development, a new one gets HR.
        await using (var db = CreateContext(NoaId))
        {
            var noa = await db.Users.SingleAsync(u => u.Id == NoaId);
            noa.DepartmentId = HrId;
            await db.SaveChangesAsync();
        }

        var afterMove = await ServiceFor(NoaId).CreateAsync(Request(), [], CancellationToken.None);

        Assert.Equal(DevId, AllTickets().Single(t => t.Id == created.Id).RequesterDepartmentId);
        Assert.Equal(HrId, AllTickets().Single(t => t.Id == afterMove.Id).RequesterDepartmentId);
    }

    [Fact]
    public async Task A_new_ticket_goes_to_general_and_is_routed_with_an_sla_and_two_history_entries()
    {
        var created = await ServiceFor(NoaId).CreateAsync(
            new CreateTicketRequest { Subject = "  תלוש נובמבר  ", Description = "  " + Description + "\n" },
            [], CancellationToken.None);

        var ticket = AllTickets().Single(t => t.Id == created.Id);
        Assert.True(created.Id > 0);
        Assert.Equal("תלוש נובמבר", ticket.Subject);
        Assert.Equal(Description, ticket.Description);

        // Task 3.3: no AI yet – the fallback classifies and routes right away.
        Assert.Equal(TicketStatus.Assigned, ticket.Status);
        Assert.Equal(AiStatus.Skipped, ticket.AiStatus);
        Assert.Equal(GeneralId, ticket.CategoryId);
        Assert.Equal(ClassificationSource.Fallback, ticket.ClassificationSource);
        Assert.Null(ticket.Urgency);
        // GENERAL → HR, whose only handler is Liat.
        Assert.Equal(HrId, ticket.AssignedDepartmentId);
        Assert.Equal(LiatId, ticket.AssignedToUserId);
        // No urgency → Medium: 72 hours (spec 3.4).
        Assert.Equal(Now.AddHours(72), ticket.DueAt);
        Assert.Equal(Now, ticket.CreatedAt);
        Assert.Equal(Now, ticket.UpdatedAt);

        var history = ticket.StatusHistory.OrderBy(h => h.Id).ToList();
        Assert.Equal(2, history.Count);
        Assert.Null(history[0].PreviousStatus);
        Assert.Equal(TicketStatus.New, history[0].NewStatus);
        Assert.Equal(NoaId, history[0].ChangedByUserId);
        Assert.Equal(Now, history[0].ChangedAt);
        Assert.Equal(TicketStatus.New, history[1].PreviousStatus);
        Assert.Equal(TicketStatus.Assigned, history[1].NewStatus);
        Assert.Null(history[1].ChangedByUserId); // the system
        Assert.Equal(Now, history[1].ChangedAt);
    }

    [Fact]
    public async Task The_ticket_its_routing_and_its_history_are_saved_in_one_save()
    {
        CountingUnitOfWork? counting = null;
        UnitOfWorkFactory = db => counting = new CountingUnitOfWork(new UnitOfWork(db));

        await ServiceFor(NoaId).CreateAsync(new CreateTicketRequest { Description = Description }, [],
            CancellationToken.None);

        Assert.Equal(1, counting!.SaveCount);
        Assert.Equal(0, counting.TransactionCount);
    }

    [Fact]
    public async Task When_the_save_fails_neither_the_ticket_nor_its_history_is_stored()
    {
        UnitOfWorkFactory = _ => new CountingUnitOfWork(fail: true);
        var before = AllTickets().Count;

        await Assert.ThrowsAsync<InvalidOperationException>(() => ServiceFor(NoaId).CreateAsync(
            new CreateTicketRequest { Description = Description }, [], CancellationToken.None));

        Assert.Equal(before, AllTickets().Count);
        Assert.Empty(AllTickets().SelectMany(t => t.StatusHistory).Where(h => h.ChangedAt == Now));
    }

    [Fact]
    public async Task Routing_is_logged_with_ids_and_reason_only()
    {
        var created = await ServiceFor(NoaId).CreateAsync(
            new CreateTicketRequest { Subject = "תלוש", Description = Description }, [], CancellationToken.None);

        var line = Assert.Single(Logger.Entries, e => e.Message.Contains("routed")).Message;
        Assert.Contains($"Ticket {created.Id} routed to department {HrId}, handler {LiatId}", line);
        Assert.Contains("Fallback", line);
        Assert.DoesNotContain(Description, Logger.AllText);
        Assert.DoesNotContain("תלוש", Logger.AllText);
    }

    [Fact]
    public async Task Without_an_active_fallback_category_the_ticket_is_still_saved_new_and_unrouted()
    {
        using (var db = CreateContext(signedInUserId: null))
        {
            db.Categories.IgnoreQueryFilters().Single(c => c.Id == GeneralId).IsActive = false;
            db.SaveChanges();
        }

        var created = await ServiceFor(NoaId).CreateAsync(new CreateTicketRequest { Description = Description }, [],
            CancellationToken.None);

        var ticket = AllTickets().Single(t => t.Id == created.Id);
        Assert.Equal(TicketStatus.New, ticket.Status);
        Assert.Null(ticket.AssignedDepartmentId);
        Assert.Null(ticket.DueAt);
        Assert.Single(ticket.StatusHistory);
    }

    private sealed class CountingUnitOfWork(IUnitOfWork? inner = null, bool fail = false) : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public int TransactionCount { get; private set; }

        public void DiscardChanges() => inner?.DiscardChanges();

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return fail || inner is null
                ? throw new InvalidOperationException("Database unavailable.")
                : inner.SaveChangesAsync(cancellationToken);
        }

        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action,
            CancellationToken cancellationToken)
        {
            TransactionCount++;
            return inner!.ExecuteInTransactionAsync(action, cancellationToken);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_subject_is_stored_as_null(string? subject)
    {
        var created = await ServiceFor(NoaId).CreateAsync(
            new CreateTicketRequest { Subject = subject, Description = Description }, [], CancellationToken.None);

        Assert.Null(AllTickets().Single(t => t.Id == created.Id).Subject);
    }

    [Fact]
    public async Task An_inactive_user_with_a_still_valid_token_cannot_open_a_ticket()
    {
        var error = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            ServiceFor(OritId).CreateAsync(Request(), [], CancellationToken.None));

        Assert.Equal(AuthenticationFailedException.NotSignedInTitle, error.Title);
        Assert.DoesNotContain(AllTickets(), t => t.RequesterId == OritId);
    }

    [Fact]
    public async Task An_invalid_request_saves_nothing()
    {
        var before = AllTickets().Count;

        await Assert.ThrowsAsync<ValidationException>(() =>
            ServiceFor(NoaId).CreateAsync(new CreateTicketRequest { Description = "קצר" }, [], CancellationToken.None));

        Assert.Equal(before, AllTickets().Count);
    }

    [Fact]
    public async Task The_log_has_ids_only_never_the_subject_or_description()
    {
        var created = await ServiceFor(NoaId).CreateAsync(
            new CreateTicketRequest { Subject = "נושא סודי", Description = Description }, [], CancellationToken.None);

        var log = Logger.AllText;
        Assert.Contains(created.Id.ToString(), log);
        Assert.Contains(NoaId.ToString(), log);
        Assert.DoesNotContain("נושא סודי", log);
        Assert.DoesNotContain(Description, log);
    }

    private static CreateTicketRequest Request() => new() { Subject = "תלוש", Description = Description };
}
