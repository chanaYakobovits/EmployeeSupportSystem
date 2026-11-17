using Common.DTOs.Tickets;
using Common.Enums;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Tests.Ai;

/// <summary>Opening a ticket (spec 4.3): an analysis job when the AI is available, GENERAL at once otherwise (spec 5.6).</summary>
public sealed class CreateTicketWithAiTests : AiTestBase
{
    private static CreateTicketRequest Request() => new() { Subject = "תלוש", Description = "לא קיבלתי את תלוש השכר" };

    [Fact]
    public async Task With_the_AI_available_the_ticket_stays_New_with_a_pending_job_in_the_same_save()
    {
        AiGate.Value = AiAvailability.Available;

        var created = await ServiceFor(NoaId).CreateAsync(Request(), [], CancellationToken.None);

        var ticket = TicketRow(created.Id);
        Assert.Equal((TicketStatus.New, AiStatus.Pending), (ticket.Status, ticket.AiStatus));
        Assert.Null(ticket.CategoryId);
        Assert.Null(ticket.ClassificationSource);
        Assert.Null(ticket.AssignedDepartmentId);
        Assert.Null(ticket.DueAt);
        Assert.Single(ticket.StatusHistory); // the opening row only

        using var db = SystemContext();
        var job = Assert.Single(db.AiAnalysisJobs.AsNoTracking().Where(j => j.TicketId == created.Id));
        Assert.Equal((OrgA, AiJobStatus.Pending, 0, Now), (job.OrganizationId, job.Status, job.Attempts, job.NextAttemptAt));
    }

    [Theory]
    [InlineData(AiAvailability.Disabled)]
    [InlineData(AiAvailability.CircuitOpen)]
    public async Task Without_the_AI_the_ticket_goes_to_GENERAL_at_once_without_a_job(AiAvailability availability)
    {
        AiGate.Value = availability;

        var created = await ServiceFor(NoaId).CreateAsync(Request(), [], CancellationToken.None);

        var ticket = TicketRow(created.Id);
        Assert.Equal((TicketStatus.Assigned, AiStatus.Skipped, GeneralId, ClassificationSource.Fallback),
            (ticket.Status, ticket.AiStatus, ticket.CategoryId!.Value, ticket.ClassificationSource!.Value));
        Assert.Null(ticket.Urgency);

        using var db = SystemContext();
        Assert.Empty(db.AiAnalysisJobs.Where(j => j.TicketId == created.Id));
        Assert.Contains($"AI analysis skipped ({availability})", Logger.AllText);
    }

    [Fact]
    public async Task While_the_breaker_is_open_new_tickets_go_to_GENERAL_and_after_it_closes_they_are_queued()
    {
        var check = new Services.Implementations.Ai.AiAvailabilityCheck(Microsoft.Extensions.Options.Options.Create(Ai), Breaker);
        for (var i = 0; i < 5; i++)
        {
            Breaker.RecordFailure();
        }

        AiGate.Value = check.Check();
        var duringOutage = await ServiceFor(NoaId).CreateAsync(Request(), [], CancellationToken.None);
        Assert.Equal(GeneralId, TicketRow(duringOutage.Id).CategoryId);

        Clock.Advance(TimeSpan.FromMinutes(5));
        AiGate.Value = check.Check();
        var afterwards = await ServiceFor(NoaId).CreateAsync(Request(), [], CancellationToken.None);
        Assert.Equal(AiStatus.Pending, TicketRow(afterwards.Id).AiStatus);
    }

    [Fact]
    public async Task A_created_ticket_is_then_analyzed_by_the_worker()
    {
        AiGate.Value = AiAvailability.Available;
        var created = await ServiceFor(NoaId).CreateAsync(Request(), [], CancellationToken.None);
        AiClient.Returns(Fakes.FakeAiClient.Analysis("PAYROLL", Urgency.Medium));

        using (var db = SystemContext())
        {
            await RunAsync(db.AiAnalysisJobs.Single(j => j.TicketId == created.Id).Id);
        }

        var ticket = TicketRow(created.Id);
        Assert.Equal((PayrollId, ClassificationSource.Ai, AlonId, TicketStatus.Assigned),
            (ticket.CategoryId!.Value, ticket.ClassificationSource!.Value, ticket.AssignedToUserId!.Value, ticket.Status));
        Assert.Equal(2, ticket.StatusHistory.Count); // opened by the requester, assigned by the system
    }
}
