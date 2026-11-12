using System.Text.Json;
using Common.DTOs.Tickets;
using Common.Enums;
using Common.Exceptions;
using Microsoft.EntityFrameworkCore;

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

        var created = await ServiceFor(NoaId).CreateAsync(request, CancellationToken.None);

        var ticket = AllTickets().Single(t => t.Id == created.Id);
        Assert.Equal(NoaId, ticket.RequesterId);
        Assert.Equal(DevId, ticket.RequesterDepartmentId);
        Assert.Equal(OrgA, ticket.OrganizationId);
    }

    [Fact]
    public async Task The_department_is_read_from_the_database_and_kept_as_a_snapshot()
    {
        // The token's department claim (1) is stale; the database says Development.
        var created = await ServiceFor(NoaId).CreateAsync(Request(), CancellationToken.None);
        Assert.NotEqual(ClaimDepartmentId, DevId);
        Assert.Equal(DevId, AllTickets().Single(t => t.Id == created.Id).RequesterDepartmentId);

        // Noa moves to HR: her existing ticket keeps Development, a new one gets HR.
        await using (var db = CreateContext(NoaId))
        {
            var noa = await db.Users.SingleAsync(u => u.Id == NoaId);
            noa.DepartmentId = HrId;
            await db.SaveChangesAsync();
        }

        var afterMove = await ServiceFor(NoaId).CreateAsync(Request(), CancellationToken.None);

        Assert.Equal(DevId, AllTickets().Single(t => t.Id == created.Id).RequesterDepartmentId);
        Assert.Equal(HrId, AllTickets().Single(t => t.Id == afterMove.Id).RequesterDepartmentId);
    }

    [Fact]
    public async Task A_new_ticket_is_New_unclassified_unrouted_with_an_opening_history_entry()
    {
        var created = await ServiceFor(NoaId).CreateAsync(
            new CreateTicketRequest { Subject = "  תלוש נובמבר  ", Description = "  " + Description + "\n" },
            CancellationToken.None);

        var ticket = AllTickets().Single(t => t.Id == created.Id);
        Assert.True(created.Id > 0);
        Assert.Equal("תלוש נובמבר", ticket.Subject);
        Assert.Equal(Description, ticket.Description);
        Assert.Equal(TicketStatus.New, ticket.Status);
        Assert.Equal(AiStatus.Pending, ticket.AiStatus);
        Assert.Null(ticket.CategoryId);
        Assert.Null(ticket.Urgency);
        Assert.Null(ticket.ClassificationSource);
        Assert.Null(ticket.AssignedDepartmentId);
        Assert.Null(ticket.AssignedToUserId);
        Assert.Null(ticket.DueAt);
        Assert.Equal(Now, ticket.CreatedAt);
        Assert.Equal(Now, ticket.UpdatedAt);

        var history = Assert.Single(ticket.StatusHistory);
        Assert.Null(history.PreviousStatus);
        Assert.Equal(TicketStatus.New, history.NewStatus);
        Assert.Equal(NoaId, history.ChangedByUserId);
        Assert.Equal(Now, history.ChangedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_subject_is_stored_as_null(string? subject)
    {
        var created = await ServiceFor(NoaId).CreateAsync(
            new CreateTicketRequest { Subject = subject, Description = Description }, CancellationToken.None);

        Assert.Null(AllTickets().Single(t => t.Id == created.Id).Subject);
    }

    [Fact]
    public async Task An_inactive_user_with_a_still_valid_token_cannot_open_a_ticket()
    {
        var error = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            ServiceFor(OritId).CreateAsync(Request(), CancellationToken.None));

        Assert.Equal(AuthenticationFailedException.NotSignedInTitle, error.Title);
        Assert.DoesNotContain(AllTickets(), t => t.RequesterId == OritId);
    }

    [Fact]
    public async Task An_invalid_request_saves_nothing()
    {
        var before = AllTickets().Count;

        await Assert.ThrowsAsync<ValidationException>(() =>
            ServiceFor(NoaId).CreateAsync(new CreateTicketRequest { Description = "קצר" }, CancellationToken.None));

        Assert.Equal(before, AllTickets().Count);
    }

    [Fact]
    public async Task The_log_has_ids_only_never_the_subject_or_description()
    {
        var created = await ServiceFor(NoaId).CreateAsync(
            new CreateTicketRequest { Subject = "נושא סודי", Description = Description }, CancellationToken.None);

        var log = Logger.AllText;
        Assert.Contains(created.Id.ToString(), log);
        Assert.Contains(NoaId.ToString(), log);
        Assert.DoesNotContain("נושא סודי", log);
        Assert.DoesNotContain(Description, log);
    }

    private static CreateTicketRequest Request() => new() { Subject = "תלוש", Description = Description };
}
