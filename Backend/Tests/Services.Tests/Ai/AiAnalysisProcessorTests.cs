using Common.Constants;
using Common.Enums;
using Common.Exceptions;
using Repositories.Interfaces;
using Services.Tests.Ai.Fakes;

namespace Services.Tests.Ai;

/// <summary>The AI worker's processing of one job (task 4.3, spec 4.3, 5.2–5.6, 11.2).</summary>
public sealed class AiAnalysisProcessorTests : AiTestBase
{
    // ===================== Results =====================

    [Fact]
    public async Task Valid_result_classifies_routes_and_assigns_by_the_AI()
    {
        var (ticketId, jobId) = AddQueuedTicket();
        AiClient.Returns(FakeAiClient.Analysis("PAYROLL", Urgency.High));

        await RunAsync(jobId);

        var ticket = TicketRow(ticketId);
        Assert.Equal(PayrollId, ticket.CategoryId);
        Assert.Equal(Urgency.High, ticket.Urgency);
        Assert.Equal(ClassificationSource.Ai, ticket.ClassificationSource);
        Assert.Equal(DevId, ticket.AssignedDepartmentId);
        Assert.Equal(AlonId, ticket.AssignedToUserId);
        Assert.Equal(TicketStatus.Assigned, ticket.Status);
        Assert.Equal(AiStatus.Completed, ticket.AiStatus);
        Assert.Equal(Now.AddHours(SlaDefaults.Hours[Urgency.High]), ticket.DueAt);

        // The routing is a system action in the timeline.
        var row = Assert.Single(ticket.StatusHistory);
        Assert.Equal((TicketStatus.New, TicketStatus.Assigned, (int?)null),
            (row.PreviousStatus!.Value, row.NewStatus, row.ChangedByUserId));

        var job = JobRow(jobId);
        Assert.Equal(AiJobStatus.Succeeded, job.Status);
        Assert.Equal(Now, job.CompletedAt);
        Assert.Null(job.LockedUntil);

        var analysis = AnalysisRow(ticketId)!;
        Assert.Equal(("PAYROLL", Urgency.High, 0.9m), (analysis.CategoryCode, analysis.Urgency, analysis.Confidence));
        Assert.Equal((FakeAiClient.Model, FakeAiClient.PromptVersion), (analysis.Model, analysis.PromptVersion));
        Assert.Equal((321, 123, 1500), (analysis.InputTokens, analysis.OutputTokens, analysis.DurationMs));
        Assert.Equal("[\"חודש התלוש\"]", analysis.MissingInfoJson);
        Assert.Equal(AiClient.SentTexts.Single(), analysis.RedactedInput);
    }

    [Fact]
    public async Task Only_active_categories_are_offered_to_the_AI()
    {
        AddCategory(50, "OLD", "ישנה", DevId, isActive: false);
        var (_, jobId) = AddQueuedTicket();
        AiClient.Returns(FakeAiClient.Analysis("PAYROLL"));

        await RunAsync(jobId);

        Assert.Equal(["GENERAL", "PAYROLL"], AiClient.SentCategories.Single().Select(c => c.Code).Order());
    }

    [Theory]
    [InlineData(AiValidationVerdict.LowConfidence, "PAYROLL")]
    [InlineData(AiValidationVerdict.UnknownCategory, "NO_SUCH")]
    public async Task Low_confidence_or_unknown_category_goes_to_GENERAL_and_keeps_the_analysis(
        AiValidationVerdict verdict, string code)
    {
        var (ticketId, jobId) = AddQueuedTicket();
        AiClient.Returns(FakeAiClient.Analysis(code, Urgency.Critical, 0.4m, verdict));

        await RunAsync(jobId);

        var ticket = TicketRow(ticketId);
        Assert.Equal(GeneralId, ticket.CategoryId);
        Assert.Null(ticket.Urgency);
        Assert.Equal(ClassificationSource.Fallback, ticket.ClassificationSource);
        Assert.Equal((HrId, (int?)LiatId), (ticket.AssignedDepartmentId!.Value, ticket.AssignedToUserId));
        Assert.Equal(TicketStatus.Assigned, ticket.Status);
        Assert.Equal(AiStatus.Completed, ticket.AiStatus);
        // No urgency → the SLA uses Medium (spec 3.4).
        Assert.Equal(Now.AddHours(SlaDefaults.Hours[Urgency.Medium]), ticket.DueAt);

        // The recommendation for the handler (spec 5.5).
        var analysis = AnalysisRow(ticketId)!;
        Assert.Equal((code, Urgency.Critical, 0.4m), (analysis.CategoryCode, analysis.Urgency, analysis.Confidence));
        Assert.Equal(AiJobStatus.Succeeded, JobRow(jobId).Status);
    }

    [Fact]
    public async Task A_valid_result_with_a_category_that_is_not_active_now_goes_to_GENERAL()
    {
        var (ticketId, jobId) = AddQueuedTicket();
        // Offered when the call was made; the result names a code that is no longer in the active list.
        AiClient.Returns(FakeAiClient.Analysis("TRAVEL"));

        await RunAsync(jobId);

        Assert.Equal((GeneralId, ClassificationSource.Fallback),
            (TicketRow(ticketId).CategoryId!.Value, TicketRow(ticketId).ClassificationSource!.Value));
        Assert.Equal("TRAVEL", AnalysisRow(ticketId)!.CategoryCode);
    }

    // ===================== Failures and retries =====================

    [Fact]
    public async Task Transient_failures_retry_after_30s_2m_10m_and_then_go_to_GENERAL()
    {
        var (ticketId, jobId) = AddQueuedTicket();
        AiClient.Returns(FakeAiClient.Failure(AiErrorCode.Timeout));

        foreach (var delay in new[] { 30, 120, 600 })
        {
            await RunAsync(jobId);

            var job = JobRow(jobId);
            Assert.Equal(AiJobStatus.Pending, job.Status);
            Assert.Equal(Clock.UtcNow.AddSeconds(delay), job.NextAttemptAt);
            Assert.Equal("Timeout", job.LastErrorCode);
            Assert.Null(job.LockedUntil);
            Assert.Equal((TicketStatus.New, AiStatus.Pending), (TicketRow(ticketId).Status, TicketRow(ticketId).AiStatus));

            Assert.False(Claim(jobId)); // not due yet
            Clock.Advance(TimeSpan.FromSeconds(delay));
        }

        // The first attempt and three retries, all failed.
        await RunAsync(jobId);

        Assert.Equal(4, AiClient.Calls);
        var ticket = TicketRow(ticketId);
        Assert.Equal((GeneralId, ClassificationSource.Fallback, TicketStatus.Assigned, AiStatus.Failed),
            (ticket.CategoryId!.Value, ticket.ClassificationSource!.Value, ticket.Status, ticket.AiStatus));
        Assert.Null(ticket.Urgency);
        var finalJob = JobRow(jobId);
        Assert.Equal((AiJobStatus.Failed, "Timeout", 4), (finalJob.Status, finalJob.LastErrorCode, finalJob.Attempts));
        Assert.Null(AnalysisRow(ticketId));
    }

    [Fact]
    public async Task A_retry_is_never_scheduled_after_the_time_guard()
    {
        // Opened 14:50 minutes ago: the 30s retry would come after the 15 minutes.
        var (ticketId, jobId) = AddQueuedTicket(createdAt: Now.AddSeconds(-890));
        AiClient.Returns(FakeAiClient.Failure(AiErrorCode.ServerError));

        await RunAsync(jobId);
        Assert.Equal(Now.AddSeconds(10), JobRow(jobId).NextAttemptAt);

        Clock.Advance(TimeSpan.FromSeconds(10));
        await RunAsync(jobId);

        Assert.Equal(1, AiClient.Calls);
        Assert.Equal(AiJobErrorCodes.FallbackTimeout, JobRow(jobId).LastErrorCode);
        Assert.Equal(GeneralId, TicketRow(ticketId).CategoryId);
    }

    [Theory]
    [InlineData(AiErrorCode.Rejected)]
    [InlineData(AiErrorCode.Disabled)]
    public async Task Final_failures_go_to_GENERAL_without_a_retry(AiErrorCode code)
    {
        var (ticketId, jobId) = AddQueuedTicket();
        AiClient.Returns(FakeAiClient.Failure(code));

        await RunAsync(jobId);

        Assert.Equal(1, AiClient.Calls);
        var job = JobRow(jobId);
        Assert.Equal((AiJobStatus.Failed, code.ToString(), 1), (job.Status, job.LastErrorCode, job.Attempts));
        var ticket = TicketRow(ticketId);
        Assert.Equal((GeneralId, ClassificationSource.Fallback, TicketStatus.Assigned, AiStatus.Failed),
            (ticket.CategoryId!.Value, ticket.ClassificationSource!.Value, ticket.Status, ticket.AiStatus));
    }

    [Fact]
    public async Task Fifteen_minutes_after_the_opening_the_ticket_goes_to_GENERAL_without_a_call()
    {
        var (ticketId, jobId) = AddQueuedTicket(createdAt: Now.AddMinutes(-15));
        AiClient.Returns(FakeAiClient.Analysis("PAYROLL"));

        await RunAsync(jobId);

        Assert.Equal(0, AiClient.Calls);
        Assert.Equal((AiJobStatus.Failed, AiJobErrorCodes.FallbackTimeout),
            (JobRow(jobId).Status, JobRow(jobId).LastErrorCode));
        var ticket = TicketRow(ticketId);
        Assert.Equal((GeneralId, TicketStatus.Assigned, AiStatus.Failed),
            (ticket.CategoryId!.Value, ticket.Status, ticket.AiStatus));
    }

    [Fact]
    public async Task Fourteen_minutes_after_the_opening_the_AI_is_still_called()
    {
        var (ticketId, jobId) = AddQueuedTicket(createdAt: Now.AddMinutes(-14));
        AiClient.Returns(FakeAiClient.Analysis("PAYROLL"));

        await RunAsync(jobId);

        Assert.Equal(ClassificationSource.Ai, TicketRow(ticketId).ClassificationSource);
    }

    [Fact]
    public async Task A_job_claimed_too_often_is_abandoned_to_GENERAL()
    {
        var (ticketId, jobId) = AddQueuedTicket();
        // Earlier claims crashed without a result: 4 claims so far, this one is the 5th (> 1 + MaxAttempts).
        using (var db = SystemContext())
        {
            db.AiAnalysisJobs.Single(j => j.Id == jobId).Attempts = 4;
            db.SaveChanges();
        }

        await RunAsync(jobId);

        Assert.Equal(0, AiClient.Calls);
        Assert.Equal(AiJobErrorCodes.Abandoned, JobRow(jobId).LastErrorCode);
        Assert.Equal(GeneralId, TicketRow(ticketId).CategoryId);
    }

    // ===================== Circuit breaker =====================

    [Fact]
    public async Task Five_failed_jobs_in_a_row_open_the_breaker_and_the_next_job_goes_to_GENERAL_without_a_call()
    {
        AiClient.Returns(FakeAiClient.Failure(AiErrorCode.Rejected));
        for (var i = 0; i < 5; i++)
        {
            await RunAsync(AddQueuedTicket().JobId);
        }

        Assert.True(Breaker.IsOpen);
        var (ticketId, jobId) = AddQueuedTicket();
        await RunAsync(jobId);

        Assert.Equal(5, AiClient.Calls);
        Assert.Equal(AiJobErrorCodes.CircuitOpen, JobRow(jobId).LastErrorCode);
        Assert.Equal((GeneralId, AiStatus.Failed), (TicketRow(ticketId).CategoryId!.Value, TicketRow(ticketId).AiStatus));
    }

    [Fact]
    public async Task Disabled_is_not_a_failure_of_the_breaker()
    {
        AiClient.Returns(FakeAiClient.Failure(AiErrorCode.Disabled));
        for (var i = 0; i < 6; i++)
        {
            await RunAsync(AddQueuedTicket().JobId);
        }

        Assert.False(Breaker.IsOpen);
        Assert.Equal(6, AiClient.Calls);
    }

    // ===================== People were faster (spec 11.2) =====================

    [Fact]
    public async Task A_manual_classification_before_the_result_is_kept_and_the_analysis_is_a_recommendation()
    {
        var (ticketId, jobId) = AddQueuedTicket();
        AiClient.Returns(FakeAiClient.Analysis("GENERAL", Urgency.Low));
        AiClient.OnCall = () => ChangeTicket(ticketId, t =>
        {
            // A handler reclassified while the AI was working.
            t.CategoryId = PayrollId;
            t.Urgency = Urgency.Critical;
            t.ClassificationSource = ClassificationSource.Manual;
            t.Status = TicketStatus.Assigned;
            t.AssignedDepartmentId = DevId;
            t.AssignedToUserId = AlonId;
        });

        await RunAsync(jobId);

        var ticket = TicketRow(ticketId);
        Assert.Equal((PayrollId, Urgency.Critical, ClassificationSource.Manual, TicketStatus.Assigned, AlonId),
            (ticket.CategoryId!.Value, ticket.Urgency!.Value, ticket.ClassificationSource!.Value, ticket.Status,
                ticket.AssignedToUserId!.Value));
        Assert.Empty(ticket.StatusHistory); // no system routing row
        Assert.Equal(AiStatus.Completed, ticket.AiStatus);
        Assert.Equal(("GENERAL", Urgency.Low), (AnalysisRow(ticketId)!.CategoryCode, AnalysisRow(ticketId)!.Urgency));
        Assert.Equal(AiJobStatus.Succeeded, JobRow(jobId).Status);
        Assert.Contains("recommendation only", ProcessorLogger.AllText);
    }

    [Fact]
    public async Task A_conflict_on_the_save_is_applied_again_to_the_new_state()
    {
        var (ticketId, jobId) = AddQueuedTicket();
        AiClient.Returns(FakeAiClient.Analysis("PAYROLL"));
        // Saves: 1 = AiStatus Processing, 2 = the result. The 2nd conflicts: the employee cancelled meanwhile.
        UnitOfWorkFactory = db => new ConflictOnSave(RealUnitOfWork(db), failingSave: 2,
            before: () => ChangeTicket(ticketId, t => t.Status = TicketStatus.Cancelled));

        await RunAsync(jobId);

        var ticket = TicketRow(ticketId);
        Assert.Equal(TicketStatus.Cancelled, ticket.Status);
        Assert.Null(ticket.CategoryId);
        Assert.Equal(AiStatus.Completed, ticket.AiStatus);
        Assert.NotNull(AnalysisRow(ticketId));
        Assert.Equal(AiJobStatus.Succeeded, JobRow(jobId).Status);
    }

    [Fact]
    public async Task A_closed_or_cancelled_ticket_ends_the_job_without_a_call()
    {
        var (ticketId, jobId) = AddQueuedTicket();
        ChangeTicket(ticketId, t => t.Status = TicketStatus.Cancelled);

        await RunAsync(jobId);

        Assert.Equal(0, AiClient.Calls);
        Assert.Equal((AiJobStatus.Failed, AiJobErrorCodes.TicketClosed), (JobRow(jobId).Status, JobRow(jobId).LastErrorCode));
        Assert.Null(TicketRow(ticketId).CategoryId);
    }

    // ===================== Locks =====================

    [Fact]
    public async Task A_job_whose_lock_expired_is_claimed_again_and_completed()
    {
        var (ticketId, jobId) = AddQueuedTicket();
        AiClient.Returns(FakeAiClient.Analysis("PAYROLL"));
        Assert.True(Claim(jobId)); // a server claimed it and crashed

        Assert.False(Claim(jobId)); // locked
        Clock.Advance(Processor().LockDuration + TimeSpan.FromSeconds(1));
        await RunAsync(jobId);

        Assert.Equal(2, JobRow(jobId).Attempts);
        Assert.Equal(AiJobStatus.Succeeded, JobRow(jobId).Status);
        Assert.Equal(ClassificationSource.Ai, TicketRow(ticketId).ClassificationSource);
    }

    [Fact]
    public async Task A_worker_whose_job_was_claimed_again_does_not_save_its_result()
    {
        var (ticketId, jobId) = AddQueuedTicket();
        AiClient.Returns(FakeAiClient.Analysis("PAYROLL"));
        // While the first worker waits for the AI, its lock expires and another server claims the job.
        AiClient.OnCall = () =>
        {
            Clock.Advance(Processor().LockDuration + TimeSpan.FromSeconds(1));
            Assert.True(Claim(jobId));
        };

        await RunAsync(jobId);

        var job = JobRow(jobId);
        Assert.Equal((AiJobStatus.Processing, 2), (job.Status, job.Attempts));
        Assert.Null(TicketRow(ticketId).CategoryId);
        Assert.Null(AnalysisRow(ticketId));
    }

    // ===================== Privacy (spec 1.4, 5.3, 6.4) =====================

    [Fact]
    public async Task Only_the_redacted_text_is_sent_and_it_is_stored_as_RedactedInput()
    {
        const string description =
            "שלום, אני נועה ברקוביץ. עידו שגיא אמר לי לפנות. המייל שלי noa.b@example.com והטלפון 050-1234567.";
        var (ticketId, jobId) = AddQueuedTicket(subject: "בקשה של נועה", description: description);
        AiClient.Returns(FakeAiClient.Analysis("PAYROLL"));

        await RunAsync(jobId);

        var sent = AiClient.SentTexts.Single();
        foreach (var secret in new[] { "נועה", "ברקוביץ", "עידו", "שגיא", "noa.b@example.com", "050-1234567" })
        {
            Assert.DoesNotContain(secret, sent);
        }
        Assert.Contains("[NAME]", sent);
        Assert.Contains("[EMAIL]", sent);
        Assert.Contains("[PHONE]", sent);
        Assert.StartsWith("בקשה של [NAME]", sent); // the subject, then the description
        Assert.Equal(sent, AnalysisRow(ticketId)!.RedactedInput);
    }

    [Fact]
    public async Task The_log_has_ids_and_metadata_but_no_ticket_text_or_AI_output()
    {
        var (ticketId, jobId) = AddQueuedTicket(description: "תיאור סודי של הבעיה");
        AiClient.Returns(FakeAiClient.Analysis("PAYROLL"));

        await RunAsync(jobId);

        var log = ProcessorLogger.AllText;
        Assert.Contains($"AI job {jobId} for ticket {ticketId}", log);
        Assert.Contains("InputTokens=321", log);
        Assert.DoesNotContain("סודי", log);
        Assert.DoesNotContain("תלוש שכר חסר", log); // the AI's suggested subject
        Assert.DoesNotContain("נבדוק ונחזור", log); // the AI's suggested solution
    }

    // ===================== Helpers =====================

    private static IUnitOfWork RealUnitOfWork(Repositories.Data.AppDbContext db) =>
        new Repositories.Implementations.UnitOfWork(db);

    /// <summary>Runs <paramref name="before"/> and throws a concurrency conflict on save number <paramref name="failingSave"/>.</summary>
    private sealed class ConflictOnSave(IUnitOfWork inner, int failingSave, Action before) : IUnitOfWork
    {
        private int _saves;

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (++_saves != failingSave)
            {
                return inner.SaveChangesAsync(cancellationToken);
            }

            before();
            return Task.FromException(new ConflictException(errorType: "concurrency"));
        }

        public void DiscardChanges() => inner.DiscardChanges();

        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action,
            CancellationToken cancellationToken) => inner.ExecuteInTransactionAsync(action, cancellationToken);
    }
}
