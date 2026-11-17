using Common.Constants;
using Common.DTOs.Ai;
using Common.DTOs.Tickets;
using Common.Enums;
using Common.Exceptions;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Repositories.Implementations;
using Services.Implementations.Ai;
using Services.Implementations.Tickets;
using Services.Interfaces;
using Services.Tests.Ai.Fakes;
using Services.Tests.Auth.Fakes;
using Services.Tests.Fakes;

namespace Services.Tests.Ai;

/// <summary>
/// The handler's view of the AI analysis and "נתח מחדש" (task 4.4): who sees it, what "applied" means in each case, and
/// that a re-analysis of a routed ticket is a recommendation only.
/// </summary>
public sealed class TicketAiAnalysisServiceTests : AiTestBase
{
    public TicketAiAnalysisServiceTests()
    {
        AiGate.Value = AiAvailability.Available;
    }

    // ===================== GET: who =====================

    [Fact]
    public async Task The_requester_gets_404()
    {
        var (ticketId, _) = AddQueuedTicket();

        await Assert.ThrowsAsync<NotFoundException>(() => Service(NoaId).GetAsync(ticketId, CancellationToken.None));
    }

    [Fact]
    public async Task A_requester_who_is_staff_gets_404_on_their_own_ticket()
    {
        // Alon is a handler of Development – and opened this one himself: the requester's view, no AI.
        var (ticketId, _) = AddQueuedTicket(requesterId: AlonId);

        await Assert.ThrowsAsync<NotFoundException>(() => Service(AlonId).GetAsync(ticketId, CancellationToken.None));
    }

    [Fact]
    public async Task Staff_out_of_scope_get_404()
    {
        var ticketId = AddRoutedToDevelopment();

        // Liat handles HR; the ticket is handled by Development and opened there.
        await Assert.ThrowsAsync<NotFoundException>(() => Service(LiatId).GetAsync(ticketId, CancellationToken.None));
    }

    [Theory]
    [InlineData(AlonId)]   // handler of the handling department
    [InlineData(RonId)]    // its manager
    [InlineData(AvnerId)]  // the executive
    public async Task Handlers_managers_and_the_executive_see_it(int userId)
    {
        var ticketId = AddRoutedToDevelopment();

        var dto = await Service(userId).GetAsync(ticketId, CancellationToken.None);

        Assert.Equal(AiStatus.Completed, dto.Processing.Status);
        Assert.NotNull(dto.Analysis);
    }

    // ===================== GET: what =====================

    [Fact]
    public async Task A_valid_analysis_is_applied_and_shown_in_full()
    {
        var (ticketId, jobId) = AddQueuedTicket();
        AiClient.Returns(FakeAiClient.Analysis("PAYROLL", Urgency.High, 0.9m));
        await RunAsync(jobId);

        var dto = await Service(AlonId).GetAsync(ticketId, CancellationToken.None);

        var analysis = dto.Analysis!;
        Assert.Equal(("PAYROLL", "שכר ותלושים", (int?)PayrollId), (analysis.CategoryCode, analysis.CategoryName,
            analysis.CategoryId));
        Assert.Equal((Urgency.High, 0.9m, AiConfidenceLevel.High), (analysis.Urgency, analysis.Confidence,
            analysis.ConfidenceLevel));
        Assert.Equal(("תלוש שכר חסר", "העובד לא קיבל תלוש", "שלום, נבדוק ונחזור אלייך."),
            (analysis.SuggestedSubject, analysis.Summary, analysis.SuggestedSolution));
        Assert.Equal(["חודש התלוש"], analysis.MissingInformation);
        Assert.Equal((FakeAiClient.Model, FakeAiClient.PromptVersion, Now), (analysis.Model, analysis.PromptVersion,
            analysis.CreatedAt));
        Assert.Equal((AiValidationVerdict.Valid, AiAnalysisDisposition.Applied, true, true),
            (analysis.Verdict, analysis.Disposition, analysis.Applied, analysis.MatchesCurrentClassification));
        Assert.Equal(AiClient.SentTexts.Single(), analysis.RedactedInput);
        Assert.Equal(new AiProcessingDto(AiStatus.Completed, null, null, Now, null), dto.Processing);
    }

    [Fact]
    public async Task Low_confidence_is_a_recommendation_that_can_be_applied()
    {
        var (ticketId, jobId) = AddQueuedTicket();
        AiClient.Returns(FakeAiClient.Analysis("PAYROLL", Urgency.High, 0.4m, AiValidationVerdict.LowConfidence));
        await RunAsync(jobId);

        var analysis = (await Service(LiatId).GetAsync(ticketId, CancellationToken.None)).Analysis!;

        Assert.Equal((AiAnalysisDisposition.LowConfidence, false, false), (analysis.Disposition, analysis.Applied,
            analysis.MatchesCurrentClassification));
        Assert.Equal((AiConfidenceLevel.Low, (int?)PayrollId), (analysis.ConfidenceLevel, analysis.CategoryId));
    }

    [Fact]
    public async Task An_unknown_category_has_nothing_to_apply()
    {
        var (ticketId, jobId) = AddQueuedTicket();
        AiClient.Returns(FakeAiClient.Analysis("NO_SUCH", Urgency.High, 0.9m, AiValidationVerdict.UnknownCategory));
        await RunAsync(jobId);

        var analysis = (await Service(LiatId).GetAsync(ticketId, CancellationToken.None)).Analysis!;

        Assert.Equal((AiAnalysisDisposition.UnknownCategory, false), (analysis.Disposition, analysis.Applied));
        Assert.Null(analysis.CategoryId);
        Assert.Null(analysis.CategoryName);
    }

    [Fact]
    public async Task A_ticket_classified_by_hand_meanwhile_keeps_it()
    {
        var (ticketId, jobId) = AddQueuedTicket();
        ChangeTicket(ticketId, t =>
        {
            t.CategoryId = GeneralId;
            t.ClassificationSource = ClassificationSource.Manual;
        });
        AiClient.Returns(FakeAiClient.Analysis("PAYROLL"));
        await RunAsync(jobId);

        var row = AnalysisRow(ticketId)!;
        Assert.Equal((AiValidationVerdict.Valid, AiAnalysisDisposition.ManuallyClassified), (row.Verdict, row.Disposition));
        Assert.Equal(GeneralId, TicketRow(ticketId).CategoryId);
    }

    [Fact]
    public async Task A_failure_is_explained_in_Hebrew_and_keeps_no_analysis()
    {
        var ticketId = AddTicket(NoaId, TicketStatus.Assigned, Now.AddDays(-1), configure: t =>
        {
            Route(t, HrId, LiatId, GeneralId);
            t.AiStatus = AiStatus.Failed;
            t.AiAnalysisJobs.Add(Job(AiJobStatus.Failed, Now.AddDays(-1), nameof(AiErrorCode.Timeout)));
        });

        var dto = await Service(LiatId).GetAsync(ticketId, CancellationToken.None);

        Assert.Equal(new AiProcessingDto(AiStatus.Failed, "Timeout", "שירות ה-AI לא הגיב בזמן", Now.AddDays(-1), null),
            dto.Processing);
        Assert.Null(dto.Analysis);
    }

    [Fact]
    public async Task A_skipped_analysis_says_so()
    {
        var ticketId = AddTicket(NoaId, TicketStatus.Assigned, Now.AddDays(-1), configure: t =>
        {
            Route(t, HrId, LiatId, GeneralId);
            t.AiStatus = AiStatus.Skipped;
        });

        var processing = (await Service(LiatId).GetAsync(ticketId, CancellationToken.None)).Processing;

        Assert.Equal((AiStatus.Skipped, null, AiErrorMessages.Skipped, (DateTime?)null),
            (processing.Status, processing.ErrorCode, processing.ErrorMessage, processing.RequestedAt));
    }

    // ===================== Retry =====================

    [Fact]
    public async Task Retry_queues_a_job_and_the_result_of_a_routed_ticket_is_a_recommendation_only()
    {
        var ticketId = AddFailedFallbackTicket();
        var before = TicketRow(ticketId);

        var updated = await Service(LiatId).RetryAsync(ticketId, ClientIp, CancellationToken.None);

        Assert.True(updated.CanView);
        Assert.Equal(AiStatus.Pending, TicketRow(ticketId).AiStatus);
        var job = LatestJob(ticketId);
        Assert.Equal((AiJobStatus.Pending, Now, Now, 0), (job.Status, job.CreatedAt, job.NextAttemptAt, job.Attempts));
        var audit = Assert.Single(AllAuditLogs());
        Assert.Equal((AuditActions.TicketAiRetryRequested, AuditEntityTypes.Ticket, (int?)ticketId, (int?)LiatId),
            (audit.Action, audit.EntityType, audit.EntityId, audit.UserId));

        // The ticket opened a day ago – long past the time guard of its first job; this job runs anyway.
        AiClient.Returns(FakeAiClient.Analysis("PAYROLL", Urgency.Critical, 0.95m));
        await RunAsync(job.Id);

        Assert.Equal(1, AiClient.Calls);
        var after = TicketRow(ticketId);
        Assert.Equal((GeneralId, (Urgency?)null, ClassificationSource.Fallback, HrId, (int?)LiatId, TicketStatus.Assigned),
            (after.CategoryId!.Value, after.Urgency, after.ClassificationSource!.Value, after.AssignedDepartmentId!.Value,
                after.AssignedToUserId, after.Status));
        Assert.Equal((before.DueAt, before.UpdatedAt), (after.DueAt, after.UpdatedAt));
        Assert.Equal(AiStatus.Completed, after.AiStatus);
        Assert.Equal(AiAnalysisDisposition.AlreadyRouted, AnalysisRow(ticketId)!.Disposition);

        var analysis = (await Service(LiatId).GetAsync(ticketId, CancellationToken.None)).Analysis!;
        Assert.Equal((false, (int?)PayrollId), (analysis.Applied, analysis.CategoryId));
    }

    [Fact]
    public async Task A_failed_retry_leaves_the_ticket_as_is()
    {
        var ticketId = AddFailedFallbackTicket();
        await Service(LiatId).RetryAsync(ticketId, ClientIp, CancellationToken.None);
        AiClient.Returns(FakeAiClient.Failure(AiErrorCode.Rejected));

        await RunAsync(LatestJob(ticketId).Id);

        var ticket = TicketRow(ticketId);
        Assert.Equal((AiStatus.Failed, GeneralId, (int?)LiatId), (ticket.AiStatus, ticket.CategoryId!.Value,
            ticket.AssignedToUserId));
        Assert.Null(AnalysisRow(ticketId));
    }

    [Theory]
    [InlineData(AiAvailability.Disabled)]
    [InlineData(AiAvailability.CircuitOpen)]
    public async Task Retry_when_the_AI_is_unavailable_is_422(AiAvailability availability)
    {
        var ticketId = AddFailedFallbackTicket();
        AiGate.Value = availability;

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service(LiatId).RetryAsync(ticketId, ClientIp, CancellationToken.None));

        Assert.Equal(("ai-unavailable", "שירות ה-AI אינו זמין כרגע"), (error.ErrorType, error.Title));
        Assert.Single(Jobs(ticketId));
    }

    [Fact]
    public async Task Retry_within_two_minutes_of_the_previous_job_is_429()
    {
        // The previous job failed a minute ago.
        var ticketId = AddFailedFallbackTicket(jobCreatedAt: Now.AddMinutes(-1));

        var error = await Assert.ThrowsAsync<TooManyRequestsException>(() =>
            Service(LiatId).RetryAsync(ticketId, ClientIp, CancellationToken.None));
        Assert.Equal(("ai-retry-too-soon", TimeSpan.FromMinutes(1)), (error.ErrorType, error.RetryAfter));

        Clock.Advance(TimeSpan.FromMinutes(1));
        await Service(LiatId).RetryAsync(ticketId, ClientIp, CancellationToken.None);
        Assert.Equal(2, Jobs(ticketId).Count);
    }

    [Fact]
    public async Task Retry_twice_in_a_row_the_second_is_refused()
    {
        var ticketId = AddFailedFallbackTicket();
        await Service(LiatId).RetryAsync(ticketId, ClientIp, CancellationToken.None);

        // Still waiting: in progress (422) before the two-minute limit.
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service(LiatId).RetryAsync(ticketId, ClientIp, CancellationToken.None));
        Assert.Equal("ai-analysis-in-progress", error.ErrorType);

        // Done quickly, and asked again within the two minutes: 429.
        AiClient.Returns(FakeAiClient.Failure(AiErrorCode.Rejected));
        await RunAsync(LatestJob(ticketId).Id);
        await Assert.ThrowsAsync<TooManyRequestsException>(() =>
            Service(LiatId).RetryAsync(ticketId, ClientIp, CancellationToken.None));
        Assert.Equal(2, Jobs(ticketId).Count);
    }

    [Fact]
    public async Task A_department_manager_may_see_but_not_retry()
    {
        var ticketId = AddRoutedToDevelopment(aiStatus: AiStatus.Failed);

        var error = await Assert.ThrowsAsync<ForbiddenException>(() =>
            Service(RonId).RetryAsync(ticketId, ClientIp, CancellationToken.None));

        Assert.Equal("ai-retry-not-allowed", error.ErrorType);
    }

    [Fact]
    public async Task The_requester_cannot_retry()
    {
        var ticketId = AddFailedFallbackTicket();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Service(NoaId).RetryAsync(ticketId, ClientIp, CancellationToken.None));
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task A_resolved_ticket_is_not_analyzed_again(TicketStatus status)
    {
        var ticketId = AddFailedFallbackTicket(status: status);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service(LiatId).RetryAsync(ticketId, ClientIp, CancellationToken.None));

        Assert.Equal("ticket-not-open", error.ErrorType);
    }

    // ===================== Suggested subject (spec 3.1) =====================

    [Fact]
    public async Task A_ticket_without_a_subject_gets_the_suggested_one_without_an_update()
    {
        var (ticketId, jobId) = AddQueuedTicket(subject: null);
        AiClient.Returns(FakeAiClient.Analysis("PAYROLL"));

        await RunAsync(jobId);

        var ticket = TicketRow(ticketId);
        Assert.Equal("תלוש שכר חסר", ticket.Subject);
        Assert.Equal(Now, ticket.UpdatedAt);
    }

    [Fact]
    public async Task The_employee_subject_is_never_replaced()
    {
        var (ticketId, jobId) = AddQueuedTicket(subject: "שלי");
        AiClient.Returns(FakeAiClient.Analysis("PAYROLL"));

        await RunAsync(jobId);

        Assert.Equal("שלי", TicketRow(ticketId).Subject);
    }

    [Fact]
    public async Task A_low_confidence_analysis_still_names_the_ticket()
    {
        var (ticketId, jobId) = AddQueuedTicket(subject: null);
        AiClient.Returns(FakeAiClient.Analysis("PAYROLL", Urgency.Low, 0.3m, AiValidationVerdict.LowConfidence));

        await RunAsync(jobId);

        Assert.Equal("תלוש שכר חסר", TicketRow(ticketId).Subject);
    }

    [Fact]
    public async Task A_suggested_subject_with_a_redaction_tag_is_not_used()
    {
        var (ticketId, jobId) = AddQueuedTicket(subject: null);
        AiClient.Returns(AiAnalysisResult.Success(
            new AiAnalysis("PAYROLL", Urgency.High, 0.9m, "תלוש של [NAME]", "סיכום", "פתרון", []),
            AiValidationVerdict.Valid, FakeAiClient.Model, FakeAiClient.PromptVersion, inputTokens: 1, outputTokens: 1,
            durationMs: 1, attempts: 1));

        await RunAsync(jobId);

        Assert.Null(TicketRow(ticketId).Subject);
    }

    // ===================== Helpers =====================

    private TicketAiAnalysisService Service(int userId)
    {
        var db = CreateContext(userId);
        var currentUser = new FakeCurrentUser { SignedInUserId = userId, SignedInOrganizationId = OrgA };
        return new TicketAiAnalysisService(
            new TicketAccess(new TicketRepository(db), new UserRepository(db), currentUser),
            new TicketRepository(db), new AiAnalysisJobRepository(db), new AuditLogRepository(db), AiGate,
            UnitOfWorkFor(db), Clock, currentUser, Options.Create(Ai), new CapturingLogger<TicketAiAnalysisService>());
    }

    /// <summary>
    /// Opened a day ago; the AI failed, so the fallback routed it to GENERAL → HR, Liat. AiStatus Failed.
    /// </summary>
    private int AddFailedFallbackTicket(DateTime? jobCreatedAt = null, TicketStatus status = TicketStatus.Assigned) =>
        AddTicket(NoaId, status, Now.AddDays(-1), configure: t =>
        {
            Route(t, HrId, LiatId, GeneralId);
            t.ClassificationSource = ClassificationSource.Fallback;
            t.DueAt = Now.AddDays(2);
            t.AiStatus = AiStatus.Failed;
            t.AiAnalysisJobs.Add(Job(AiJobStatus.Failed, jobCreatedAt ?? Now.AddDays(-1), nameof(AiErrorCode.Timeout)));
        });

    /// <summary>Classified by the AI to PAYROLL → Development, Alon; a stored analysis.</summary>
    private int AddRoutedToDevelopment(AiStatus aiStatus = AiStatus.Completed) =>
        AddTicket(NoaId, TicketStatus.Assigned, Now.AddDays(-1), configure: t =>
        {
            Route(t, DevId, AlonId, PayrollId);
            t.Urgency = Urgency.High;
            t.ClassificationSource = ClassificationSource.Ai;
            t.AiStatus = aiStatus;
            t.AiAnalysisJobs.Add(Job(AiJobStatus.Succeeded, Now.AddDays(-1), null));
            t.AiAnalysis = new TicketAiAnalysis
            {
                Model = "m", PromptVersion = "v", CategoryCode = "PAYROLL", Urgency = Urgency.High, Confidence = 0.9m,
                SuggestedSubject = "s", Summary = "x", SuggestedSolution = "y", MissingInfoJson = "[]",
                RedactedInput = "t", Verdict = AiValidationVerdict.Valid, Disposition = AiAnalysisDisposition.Applied,
                CreatedAt = Now.AddDays(-1),
            };
        });

    private static void Route(Ticket ticket, int departmentId, int handlerId, int categoryId)
    {
        ticket.CategoryId = categoryId;
        ticket.AssignedDepartmentId = departmentId;
        ticket.AssignedToUserId = handlerId;
    }

    private static AiAnalysisJob Job(AiJobStatus status, DateTime createdAt, string? errorCode) => new()
    {
        OrganizationId = OrgA, Status = status, Attempts = 1, NextAttemptAt = createdAt, CreatedAt = createdAt,
        CompletedAt = createdAt, LastErrorCode = errorCode,
    };

    private List<AiAnalysisJob> Jobs(int ticketId)
    {
        using var db = SystemContext();
        return db.AiAnalysisJobs.AsNoTracking().Where(j => j.TicketId == ticketId).OrderBy(j => j.Id).ToList();
    }

    private AiAnalysisJob LatestJob(int ticketId) => Jobs(ticketId).Last();
}
