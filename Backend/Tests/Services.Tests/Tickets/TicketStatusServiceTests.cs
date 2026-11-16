using Common.Constants;
using Common.DTOs.Tickets;
using Common.Enums;
using Common.Exceptions;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Repositories.Interfaces;
using Services.Implementations.Tickets;
using Services.Tests.Fakes;
using Services.Validators;

namespace Services.Tests.Tickets;

/// <summary>
/// Task 3.5: PATCH /status – the handler's and the requester's transitions, the message saved with them, one transaction,
/// the SLA pause and the reopening, the automatic resume on the employee's reply, and allowedActions.
/// </summary>
public sealed class TicketStatusServiceTests : TicketTestBase
{
    private const int DanaId = 9;   // a second handler in Development
    private const int GilId = 30;   // an inactive handler in Development
    private const string Message = "נא לצרף את תלוש השכר של אוקטובר";
    private const string Reason = "נימוק פנימי לבדיקה";

    private static readonly byte[] Version = [0, 0, 0, 0, 0, 0, 0, 7];
    private static readonly byte[] OldVersion = [0, 0, 0, 0, 0, 0, 0, 6];

    public TicketStatusServiceTests()
    {
        AddUser(DanaId, DevId, "דנה", "לוי", Role.Handler);
        AddUser(GilId, DevId, "גיל", "רז", Role.Handler, isActive: false);
    }

    // ===================== The handler =====================

    [Fact]
    public async Task Starting_a_ticket_from_the_queue_assigns_the_handler_who_starts()
    {
        var id = Ticket(handler: null, TicketStatus.Assigned);

        var result = await Change(AlonId, id, TicketStatus.InProgress);

        var ticket = Stored(id);
        Assert.Equal((TicketStatus.InProgress, (int?)AlonId), (ticket.Status, ticket.AssignedToUserId));
        Assert.Contains(ticket.StatusHistory, h => h is
            { PreviousStatus: TicketStatus.Assigned, NewStatus: TicketStatus.InProgress, ChangedByUserId: AlonId });
        var taken = Assert.Single(Changes(id));
        Assert.Equal((TicketChangeKind.Taken, (int?)null, (int?)AlonId), (taken.Kind, taken.FromUserId, taken.ToUserId));
        Assert.Equal(Now, ticket.UpdatedAt);
        Assert.True(result.CanView);
    }

    [Fact]
    public async Task The_personal_handler_starts_without_a_change_row()
    {
        var id = Ticket(AlonId, TicketStatus.Assigned);

        await Change(AlonId, id, TicketStatus.InProgress);

        Assert.Equal(TicketStatus.InProgress, Stored(id).Status);
        Assert.Empty(Changes(id));
    }

    [Theory]
    [InlineData(TicketStatus.Assigned, TicketStatus.InProgress)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Resolved)]
    [InlineData(TicketStatus.InProgress, TicketStatus.WaitingForEmployee)]
    public async Task A_colleague_does_not_change_the_status_of_another_handlers_ticket(TicketStatus from, TicketStatus to)
    {
        var id = Ticket(AlonId, from);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Change(DanaId, id, to, Message));

        Assert.Equal("ticket-assigned-to-other", error.ErrorType);
        Assert.Equal(from, Stored(id).Status);
    }

    [Fact]
    public async Task Only_starting_is_possible_on_a_ticket_without_a_personal_handler()
    {
        // In progress without a handler (e.g. old data): the handler takes it first (POST /assign).
        var id = Ticket(handler: null, TicketStatus.InProgress);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Change(AlonId, id, TicketStatus.Resolved, Message));

        Assert.Equal("ticket-not-assigned", error.ErrorType);
    }

    [Theory]
    [InlineData(TicketStatus.WaitingForEmployee, ChangeTicketStatusRequestValidator.InfoRequestRequiredMessage)]
    [InlineData(TicketStatus.Resolved, ChangeTicketStatusRequestValidator.ResolutionRequiredMessage)]
    public async Task Asking_for_information_or_resolving_requires_a_message(TicketStatus to, string expected)
    {
        var id = Ticket(AlonId, TicketStatus.InProgress);

        var error = await Assert.ThrowsAsync<ValidationException>(() => Change(AlonId, id, to, message: "   "));

        Assert.Equal([expected], error.Errors["message"]);
        Assert.Equal(TicketStatus.InProgress, Stored(id).Status);
    }

    [Fact]
    public async Task Resolving_saves_the_resolution_reply_the_status_and_its_time_together()
    {
        var id = Ticket(AlonId, TicketStatus.InProgress);

        await Change(AlonId, id, TicketStatus.Resolved, "  יש לעדכן את הכתובת בפורטל  ", Reason);

        var ticket = Stored(id);
        Assert.Equal((TicketStatus.Resolved, (DateTime?)Now), (ticket.Status, ticket.ResolvedAt));
        var history = ticket.StatusHistory.Single(h => h.NewStatus == TicketStatus.Resolved);
        Assert.Equal((AlonId, Reason), (history.ChangedByUserId!.Value, history.Reason));
        var reply = Assert.Single(AllComments().Where(c => c.TicketId == id));
        Assert.Equal(("יש לעדכן את הכתובת בפורטל", true, false, AlonId),
            (reply.Body, reply.IsResolution, reply.IsInternal, reply.AuthorUserId));
    }

    [Fact]
    public async Task A_failed_save_keeps_neither_the_reply_nor_the_status()
    {
        var id = Ticket(AlonId, TicketStatus.InProgress);
        UnitOfWorkFactory = _ => new FailingUnitOfWork(new InvalidOperationException("database down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Change(AlonId, id, TicketStatus.Resolved, "הפתרון"));

        UnitOfWorkFactory = null;
        var ticket = Stored(id);
        Assert.Equal((TicketStatus.InProgress, (DateTime?)null), (ticket.Status, ticket.ResolvedAt));
        Assert.DoesNotContain(ticket.StatusHistory, h => h.NewStatus == TicketStatus.Resolved);
        Assert.Empty(AllComments().Where(c => c.TicketId == id));
    }

    [Fact]
    public async Task Asking_for_information_saves_a_visible_comment_and_pauses_the_clock()
    {
        var id = Ticket(AlonId, TicketStatus.InProgress);

        await Change(AlonId, id, TicketStatus.WaitingForEmployee, Message);

        var ticket = Stored(id);
        Assert.Equal((TicketStatus.WaitingForEmployee, (DateTime?)Now), (ticket.Status, ticket.SlaPausedAt));
        var comment = Assert.Single(AllComments().Where(c => c.TicketId == id));
        Assert.Equal((Message, false, false), (comment.Body, comment.IsInternal, comment.IsResolution));
    }

    [Fact]
    public async Task A_transition_outside_the_table_is_rejected_with_422()
    {
        var id = Ticket(AlonId, TicketStatus.Assigned);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Change(AlonId, id, TicketStatus.Resolved, "פתרון"));

        Assert.Equal("invalid-status-transition", error.ErrorType);
        Assert.Equal(TicketStatus.Assigned, Stored(id).Status);
        Assert.Empty(AllComments().Where(c => c.TicketId == id));
    }

    [Fact]
    public async Task The_same_status_is_not_a_transition()
    {
        var id = Ticket(AlonId, TicketStatus.InProgress);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Change(AlonId, id, TicketStatus.InProgress));

        Assert.Equal("invalid-status-transition", error.ErrorType);
    }

    [Fact]
    public async Task A_stale_version_is_a_conflict_and_nothing_changes()
    {
        var id = Ticket(AlonId, TicketStatus.InProgress);

        var error = await Assert.ThrowsAsync<ConflictException>(() => StatusFor(AlonId).ChangeStatusAsync(id,
            new ChangeTicketStatusRequest(TicketStatus.Resolved, OldVersion, Message: "פתרון"), CancellationToken.None));

        Assert.Equal(("concurrency", "הפנייה עודכנה על ידי משתמש אחר"), (error.ErrorType, error.Title));
        Assert.Equal(TicketStatus.InProgress, Stored(id).Status);
        Assert.Empty(AllComments().Where(c => c.TicketId == id));
    }

    [Theory]
    [InlineData(RonId)]     // the manager of the requester's and handling department
    [InlineData(AvnerId)]   // the executive
    public async Task A_department_manager_or_the_executive_does_not_change_the_status(int userId)
    {
        var id = Ticket(AlonId, TicketStatus.InProgress);

        var error = await Assert.ThrowsAsync<ForbiddenException>(() =>
            Change(userId, id, TicketStatus.Resolved, "פתרון"));

        Assert.Equal("status-change-not-allowed", error.ErrorType);
        Assert.Equal(TicketStatus.InProgress, Stored(id).Status);
    }

    [Fact]
    public async Task Outside_the_scope_is_not_found()
    {
        // Liat (HR) does not handle a Development ticket.
        var id = Ticket(AlonId, TicketStatus.InProgress);

        await Assert.ThrowsAsync<NotFoundException>(() => Change(LiatId, id, TicketStatus.Resolved, "פתרון"));
    }

    // ===================== The requester =====================

    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.Assigned)]
    public async Task The_requester_cancels_before_handling_started(TicketStatus from)
    {
        var id = Ticket(AlonId, from);

        await Change(NoaId, id, TicketStatus.Cancelled);

        var ticket = Stored(id);
        Assert.Equal((TicketStatus.Cancelled, (DateTime?)null), (ticket.Status, ticket.ClosedAt));
        Assert.Contains(ticket.StatusHistory, h => h is { NewStatus: TicketStatus.Cancelled, ChangedByUserId: NoaId });
    }

    [Fact]
    public async Task The_requester_does_not_cancel_once_in_progress_or_start_handling()
    {
        var inProgress = Ticket(AlonId, TicketStatus.InProgress);
        var assigned = Ticket(AlonId, TicketStatus.Assigned);

        var cancel = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Change(NoaId, inProgress, TicketStatus.Cancelled));
        var start = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Change(NoaId, assigned, TicketStatus.InProgress));

        Assert.Equal(("invalid-status-transition", "invalid-status-transition"), (cancel.ErrorType, start.ErrorType));
    }

    [Fact]
    public async Task A_reason_from_the_requester_is_rejected()
    {
        var id = Ticket(AlonId, TicketStatus.Assigned);

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            Change(NoaId, id, TicketStatus.Cancelled, reason: "סיבה פנימית"));

        Assert.Equal([TicketStatusService.ReasonNotAllowedMessage], error.Errors["reason"]);
    }

    [Fact]
    public async Task Confirming_the_resolution_closes_the_ticket()
    {
        var id = Ticket(AlonId, TicketStatus.Resolved, resolvedAt: Now.AddDays(-1));

        await Change(NoaId, id, TicketStatus.Closed);

        var ticket = Stored(id);
        Assert.Equal((TicketStatus.Closed, (DateTime?)Now), (ticket.Status, ticket.ClosedAt));
    }

    [Fact]
    public async Task Reopening_within_7_days_restarts_the_clock_from_now_and_keeps_the_resolution_in_the_history()
    {
        var id = Ticket(AlonId, TicketStatus.Resolved, urgency: Urgency.High, resolvedAt: Now.AddDays(-6));
        AddResolvedHistory(id, Now.AddDays(-6));

        await Change(NoaId, id, TicketStatus.InProgress, "הבעיה חזרה");

        var ticket = Stored(id);
        Assert.Equal((TicketStatus.InProgress, (int?)AlonId), (ticket.Status, ticket.AssignedToUserId));
        Assert.Equal(Now.AddHours(SlaDefaults.Hours[Urgency.High]), ticket.DueAt);
        Assert.Null(ticket.ResolvedAt);
        Assert.Contains(ticket.StatusHistory, h => h.NewStatus == TicketStatus.Resolved && h.ChangedAt == Now.AddDays(-6));
        var comment = Assert.Single(AllComments().Where(c => c.TicketId == id));
        Assert.Equal(("הבעיה חזרה", NoaId, false), (comment.Body, comment.AuthorUserId, comment.IsResolution));
    }

    [Fact]
    public async Task Reopening_without_urgency_uses_medium()
    {
        var id = Ticket(AlonId, TicketStatus.Resolved, urgency: null, resolvedAt: Now.AddDays(-1));

        await Change(NoaId, id, TicketStatus.InProgress);

        Assert.Equal(Now.AddHours(SlaDefaults.Hours[Urgency.Medium]), Stored(id).DueAt);
    }

    [Fact]
    public async Task Reopening_after_7_days_is_rejected()
    {
        var id = Ticket(AlonId, TicketStatus.Resolved, resolvedAt: Now.AddDays(-7));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Change(NoaId, id, TicketStatus.InProgress));

        Assert.Equal("reopen-window-expired", error.ErrorType);
        Assert.Equal(TicketStatus.Resolved, Stored(id).Status);
    }

    [Fact]
    public async Task A_ticket_reopened_after_its_handler_was_deactivated_waits_in_the_queue()
    {
        var id = Ticket(GilId, TicketStatus.Resolved, resolvedAt: Now.AddDays(-1));

        await Change(NoaId, id, TicketStatus.InProgress);

        var ticket = Stored(id);
        Assert.Equal((TicketStatus.InProgress, (int?)null, (int?)DevId),
            (ticket.Status, ticket.AssignedToUserId, ticket.AssignedDepartmentId));
    }

    // ===================== The SLA pause and the automatic resume =====================

    [Fact]
    public async Task Waiting_3_days_for_the_employee_moves_the_due_time_by_3_days()
    {
        var id = Ticket(AlonId, TicketStatus.InProgress, urgency: Urgency.Medium);
        var due = Stored(id).DueAt!.Value;

        await Change(AlonId, id, TicketStatus.WaitingForEmployee, Message);
        Clock.Advance(TimeSpan.FromDays(3));
        await ConversationFor(NoaId).AddCommentAsync(id, Comment("הנה התלוש"), [], CancellationToken.None);

        var ticket = Stored(id);
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Equal(due.AddDays(3), ticket.DueAt);
        Assert.Null(ticket.SlaPausedAt);
        var resumed = ticket.StatusHistory.Single(h => h.PreviousStatus == TicketStatus.WaitingForEmployee);
        Assert.Equal((TicketStatus.InProgress, (int?)null), (resumed.NewStatus, resumed.ChangedByUserId));
    }

    [Fact]
    public async Task A_new_urgency_during_and_after_the_wait_keeps_the_time_waited()
    {
        var id = Ticket(AlonId, TicketStatus.InProgress, urgency: Urgency.Medium);
        var created = Stored(id).CreatedAt;

        await Change(AlonId, id, TicketStatus.WaitingForEmployee, Message);
        Clock.Advance(TimeSpan.FromDays(1));
        await ReclassifyUrgency(id, Urgency.Low);
        Assert.Equal(created.AddHours(SlaDefaults.Hours[Urgency.Low]), Stored(id).DueAt);   // the wait is not over yet

        Clock.Advance(TimeSpan.FromDays(2));
        await ConversationFor(NoaId).AddAttachmentsAsync(id, [TestFiles.PdfFile()], CancellationToken.None);
        Assert.Equal(created.AddHours(SlaDefaults.Hours[Urgency.Low]).AddDays(3), Stored(id).DueAt);

        await ReclassifyUrgency(id, Urgency.High);
        Assert.Equal(created.AddHours(SlaDefaults.Hours[Urgency.High]).AddDays(3), Stored(id).DueAt);
    }

    [Fact]
    public async Task A_file_alone_from_the_requester_resumes_handling()
    {
        var id = Ticket(AlonId, TicketStatus.WaitingForEmployee, pausedAt: Now.AddHours(-2));

        await ConversationFor(NoaId).AddAttachmentsAsync(id, [TestFiles.PdfFile()], CancellationToken.None);

        Assert.Equal(TicketStatus.InProgress, Stored(id).Status);
    }

    [Theory]
    [InlineData(false)]   // the handler's visible reply
    [InlineData(true)]    // an internal note
    public async Task A_staff_comment_does_not_resume_handling(bool isInternal)
    {
        var id = Ticket(AlonId, TicketStatus.WaitingForEmployee, pausedAt: Now.AddHours(-2));

        await ConversationFor(AlonId).AddCommentAsync(id, Comment("עדיין ממתינים", isInternal), [],
            CancellationToken.None);

        var ticket = Stored(id);
        Assert.Equal((TicketStatus.WaitingForEmployee, (DateTime?)Now.AddHours(-2)), (ticket.Status, ticket.SlaPausedAt));
    }

    [Fact]
    public async Task A_requester_comment_while_resolved_changes_nothing()
    {
        var id = Ticket(AlonId, TicketStatus.Resolved, resolvedAt: Now.AddDays(-1));

        await ConversationFor(NoaId).AddCommentAsync(id, Comment("תודה"), [], CancellationToken.None);

        var ticket = Stored(id);
        Assert.Equal((TicketStatus.Resolved, (DateTime?)Now.AddDays(-1)), (ticket.Status, ticket.ResolvedAt));
    }

    // ===================== allowedActions =====================

    [Fact]
    public async Task The_requester_gets_their_actions_and_the_version()
    {
        var assigned = Ticket(AlonId, TicketStatus.Assigned);
        var resolved = Ticket(AlonId, TicketStatus.Resolved, resolvedAt: Now.AddDays(-6));
        var late = Ticket(AlonId, TicketStatus.Resolved, resolvedAt: Now.AddDays(-7));
        var closed = Ticket(AlonId, TicketStatus.Closed);

        Assert.Equal([TicketActions.Reply, TicketActions.Cancel], await RequesterActions(assigned));
        Assert.Equal([TicketActions.Reply, TicketActions.ConfirmResolved, TicketActions.Reopen],
            await RequesterActions(resolved));
        Assert.Equal([TicketActions.Reply, TicketActions.ConfirmResolved], await RequesterActions(late));
        Assert.Empty(await RequesterActions(closed));

        var details = Assert.IsType<RequesterTicketDetailsDto>(
            await ServiceFor(NoaId).GetAsync(assigned, ClientIp, CancellationToken.None));
        Assert.Equal(Version, details.RowVersion);
    }

    [Fact]
    public async Task Staff_see_the_status_actions_only_where_they_may_take_them()
    {
        var queued = Ticket(handler: null, TicketStatus.Assigned);
        var alons = Ticket(AlonId, TicketStatus.InProgress);

        Assert.Contains(TicketActions.Start, await StaffActions(AlonId, queued));
        Assert.Contains(TicketActions.Resolve, await StaffActions(AlonId, alons));
        Assert.DoesNotContain(TicketActions.Resolve, await StaffActions(DanaId, alons));
        Assert.DoesNotContain(TicketActions.RequestInfo, await StaffActions(DanaId, alons));
        Assert.DoesNotContain(TicketActions.Resolve, await StaffActions(RonId, alons));
    }

    // ===================== Logs =====================

    [Fact]
    public async Task The_log_has_the_transition_but_not_the_message_or_the_reason()
    {
        var id = Ticket(AlonId, TicketStatus.InProgress);

        await Change(AlonId, id, TicketStatus.WaitingForEmployee, Message, Reason);

        Assert.Contains($"Ticket {id} status InProgress -> WaitingForEmployee", StatusLogger.AllText);
        Assert.DoesNotContain(Message, StatusLogger.AllText);
        Assert.DoesNotContain(Reason, StatusLogger.AllText);
    }

    // ===================== Helpers =====================

    private Task<TicketUpdatedDto> Change(int userId, int ticketId, TicketStatus to, string? message = null,
        string? reason = null) =>
        StatusFor(userId).ChangeStatusAsync(ticketId, new ChangeTicketStatusRequest(to, Version, reason, message),
            CancellationToken.None);

    private async Task<IReadOnlyList<string>> RequesterActions(int ticketId) =>
        Assert.IsType<RequesterTicketDetailsDto>(
            await ServiceFor(NoaId).GetAsync(ticketId, ClientIp, CancellationToken.None)).AllowedActions;

    private async Task<IReadOnlyList<string>> StaffActions(int userId, int ticketId) =>
        Assert.IsType<StaffTicketDetailsDto>(
            await ServiceFor(userId).GetAsync(ticketId, ClientIp, CancellationToken.None)).AllowedActions;

    private async Task ReclassifyUrgency(int ticketId, Urgency urgency)
    {
        var version = Stored(ticketId).RowVersion;
        await HandlingFor(AlonId).ReclassifyAsync(ticketId,
            new ReclassifyTicketRequest(null, urgency, "שינוי דחיפות", version), CancellationToken.None);
    }

    private static CreateCommentRequest Comment(string body, bool isInternal = false) =>
        new() { Body = body, IsInternal = isInternal };

    private int Ticket(int? handler, TicketStatus status, Urgency? urgency = Urgency.Medium, DateTime? resolvedAt = null,
        DateTime? pausedAt = null) =>
        AddTicket(NoaId, status, Now.AddDays(-1), configure: t =>
        {
            t.CategoryId = PayrollId;
            t.Urgency = urgency;
            t.ClassificationSource = ClassificationSource.Manual;
            t.AssignedDepartmentId = DevId;
            t.AssignedToUserId = handler;
            t.DueAt = t.CreatedAt.AddHours(SlaDefaults.Hours[urgency ?? Urgency.Medium]);
            t.ResolvedAt = resolvedAt;
            t.SlaPausedAt = pausedAt;
            t.RowVersion = Version;
        });

    private void AddResolvedHistory(int ticketId, DateTime at)
    {
        using var db = CreateContext(signedInUserId: null);
        db.TicketStatusHistory.Add(new TicketStatusHistory
        {
            TicketId = ticketId, PreviousStatus = TicketStatus.InProgress, NewStatus = TicketStatus.Resolved,
            ChangedByUserId = AlonId, ChangedAt = at,
        });
        db.SaveChanges();
    }

    private Ticket Stored(int id) => AllTickets().Single(t => t.Id == id);

    private List<TicketChange> Changes(int id)
    {
        using var db = CreateContext(signedInUserId: null);
        return db.TicketChanges.Where(c => c.TicketId == id).OrderBy(c => c.Id).ToList();
    }

    private sealed class FailingUnitOfWork(Exception exception) : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.FromException(exception);

        public void DiscardChanges() { }

        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
