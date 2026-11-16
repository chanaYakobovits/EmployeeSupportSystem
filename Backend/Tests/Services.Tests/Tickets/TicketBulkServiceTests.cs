using Common.Constants;
using Common.DTOs.Tickets;
using Common.Enums;
using Common.Exceptions;
using Common.Models;
using Repositories.Data;
using Repositories.Implementations;
using Repositories.Interfaces;
using Services.Implementations.Tickets;
using Services.Validators;

namespace Services.Tests.Tickets;

/// <summary>
/// Task 3.6: bulk "התחל טיפול" and bulk transfer – Partial Success (spec 11.2). Each ticket through the single action,
/// with its own save; a failure of one does not undo or leak into the others.
/// </summary>
public sealed class TicketBulkServiceTests : TicketTestBase
{
    private const int DanaId = 9;          // a second handler in Development
    private const int GilId = 30;          // an inactive handler in Development
    private const int SecretariatId = 12;  // a department without handlers
    private const int ClosedDeptId = 13;   // an inactive department
    private const int MissingId = 99_999;
    private const string Reason = "העברה מרוכזת לבדיקה";

    private static readonly byte[] Version = [0, 0, 0, 0, 0, 0, 0, 7];
    private static readonly byte[] OldVersion = [0, 0, 0, 0, 0, 0, 0, 6];

    public TicketBulkServiceTests()
    {
        AddUser(DanaId, DevId, "דנה", "לוי", Role.Handler);
        AddUser(GilId, DevId, "גיל", "רז", Role.Handler, isActive: false);
        AddDepartment(SecretariatId, "מזכירות");
        AddDepartment(ClosedDeptId, "סגורה", isActive: false);
    }

    // ===================== Bulk "התחל טיפול" =====================

    [Fact]
    public async Task Partial_success_gives_each_ticket_its_own_reason_in_the_order_of_the_request()
    {
        var fromQueue = Ticket(DevId, handler: null, TicketStatus.Assigned);
        var mine = Ticket(DevId, AlonId, TicketStatus.Assigned);
        var stale = Ticket(DevId, AlonId, TicketStatus.Assigned);
        var started = Ticket(DevId, AlonId, TicketStatus.InProgress);
        var colleagues = Ticket(DevId, DanaId, TicketStatus.Assigned);

        var result = await Start(AlonId,
            Item(fromQueue), Item(stale, OldVersion), Item(MissingId), Item(mine), Item(started), Item(colleagues),
            Item(OrgBTicketId));

        Assert.Equal([fromQueue, mine], result.Succeeded);
        Assert.Equal(
        [
            new BulkFailureDto(stale, BulkFailureReason.Conflict, "concurrency"),
            new BulkFailureDto(MissingId, BulkFailureReason.NotFound, null),
            new BulkFailureDto(started, BulkFailureReason.InvalidTransition, TicketStatusTransitions.InvalidTransitionType),
            new BulkFailureDto(colleagues, BulkFailureReason.InvalidTransition, "ticket-assigned-to-other"),
            new BulkFailureDto(OrgBTicketId, BulkFailureReason.NotFound, null),
        ], result.Failed);

        // The successes went through PATCH /status: the queue's ticket is the handler's now ("Taken").
        Assert.Equal((TicketStatus.InProgress, (int?)AlonId), (Stored(fromQueue).Status, Stored(fromQueue).AssignedToUserId));
        Assert.Equal(TicketChangeKind.Taken, Assert.Single(Changes(fromQueue)).Kind);
        Assert.Equal(TicketStatus.InProgress, Stored(mine).Status);
        Assert.Contains(Stored(mine).StatusHistory, h => h is
            { PreviousStatus: TicketStatus.Assigned, NewStatus: TicketStatus.InProgress, ChangedByUserId: AlonId });

        // The failures are untouched.
        Assert.Equal(TicketStatus.Assigned, Stored(stale).Status);
        Assert.Equal((TicketStatus.Assigned, (int?)DanaId), (Stored(colleagues).Status, Stored(colleagues).AssignedToUserId));
        Assert.Equal(TicketStatus.New, Stored(OrgBTicketId).Status);
    }

    [Fact]
    public async Task A_ticket_outside_the_scope_is_not_found_like_a_missing_one()
    {
        // Handled by HR: the Development handler does not see it (404 of the single action).
        var hr = Ticket(HrId, LiatId, TicketStatus.Assigned, categoryId: GeneralId);

        var result = await Start(AlonId, Item(hr));

        Assert.Empty(result.Succeeded);
        Assert.Equal([new BulkFailureDto(hr, BulkFailureReason.NotFound, null)], result.Failed);
        Assert.Equal(TicketStatus.Assigned, Stored(hr).Status);
    }

    [Fact]
    public async Task A_failed_save_of_one_ticket_is_not_written_with_the_next_one()
    {
        var first = Ticket(DevId, AlonId, TicketStatus.Assigned);
        var second = Ticket(DevId, AlonId, TicketStatus.Assigned);
        UnitOfWorkFactory = db => new ConflictOnFirstSave(new UnitOfWork(db));

        var result = await Start(AlonId, Item(first), Item(second));

        UnitOfWorkFactory = null;
        Assert.Equal([second], result.Succeeded);
        Assert.Equal([new BulkFailureDto(first, BulkFailureReason.Conflict, "concurrency")], result.Failed);

        // Without clearing the scope, the second save would have written the first ticket's change too.
        Assert.Equal(TicketStatus.Assigned, Stored(first).Status);
        Assert.DoesNotContain(Stored(first).StatusHistory, h => h.NewStatus == TicketStatus.InProgress);
        Assert.Equal(TicketStatus.InProgress, Stored(second).Status);
    }

    [Fact]
    public async Task One_audit_record_per_request_and_logs_without_text()
    {
        var mine = Ticket(DevId, AlonId, TicketStatus.Assigned);
        var started = Ticket(DevId, AlonId, TicketStatus.InProgress);

        await Start(AlonId, Item(mine), Item(started));

        var audit = Assert.Single(AllAuditLogs());
        Assert.Equal((AuditActions.TicketBulkStatus, AuditEntityTypes.Ticket, (int?)null, (int?)AlonId, ClientIp),
            (audit.Action, audit.EntityType, audit.EntityId, audit.UserId, audit.IpAddress));
        Assert.Contains($"ticket {started} not updated", BulkLogger.AllText);
        Assert.Contains("2 requested, 1 succeeded, 1 failed", BulkLogger.AllText);
    }

    // ===================== Bulk transfer =====================

    [Fact]
    public async Task A_transfer_to_a_handler_fails_only_where_the_target_does_not_fit()
    {
        var queued = Ticket(DevId, handler: null, TicketStatus.Assigned);
        var inProgress = Ticket(DevId, AlonId, TicketStatus.InProgress);
        var danasOwn = Ticket(DevId, AlonId, TicketStatus.Assigned, requester: DanaId);
        var alreadyDanas = Ticket(DevId, DanaId, TicketStatus.Assigned);
        var resolved = Ticket(DevId, AlonId, TicketStatus.Resolved);

        var result = await Assign(AlonId, new BulkAssignRequest(DanaId, null, Reason,
            [Item(queued), Item(inProgress), Item(danasOwn), Item(alreadyDanas), Item(resolved)]));

        Assert.Equal([queued, inProgress], result.Succeeded);
        Assert.Equal(
        [
            new BulkFailureDto(danasOwn, BulkFailureReason.InvalidTarget, "assignee-is-requester"),
            new BulkFailureDto(alreadyDanas, BulkFailureReason.InvalidTarget, "assignment-unchanged"),
            new BulkFailureDto(resolved, BulkFailureReason.InvalidTransition, "ticket-not-open"),
        ], result.Failed);

        // Through POST /assign: the reason on each change row; InProgress back to Assigned for the new handler.
        var change = Assert.Single(Changes(inProgress));
        Assert.Equal((TicketChangeKind.Transferred, Reason, (int?)AlonId, (int?)DanaId),
            (change.Kind, change.Reason, change.FromUserId, change.ToUserId));
        Assert.Equal((TicketStatus.Assigned, (int?)DanaId), (Stored(inProgress).Status, Stored(inProgress).AssignedToUserId));
        Assert.Equal((int?)DanaId, Stored(queued).AssignedToUserId);
        Assert.Equal((int?)AlonId, Stored(danasOwn).AssignedToUserId);
    }

    [Fact]
    public async Task A_transfer_to_a_department_moves_every_ticket_to_its_queue()
    {
        var first = Ticket(DevId, AlonId, TicketStatus.Assigned);
        var second = Ticket(DevId, handler: null, TicketStatus.Assigned);

        var result = await Assign(AlonId, new BulkAssignRequest(null, HrId, Reason, [Item(first), Item(second)]));

        Assert.Equal([first, second], result.Succeeded);
        Assert.Empty(result.Failed);
        foreach (var id in new[] { first, second })
        {
            Assert.Equal(((int?)HrId, (int?)null), (Stored(id).AssignedDepartmentId, Stored(id).AssignedToUserId));
            Assert.Equal(Reason, Assert.Single(Changes(id)).Reason);
        }

        Assert.Equal(AuditActions.TicketBulkAssign, Assert.Single(AllAuditLogs()).Action);
        Assert.DoesNotContain(Reason, BulkLogger.AllText);
        Assert.DoesNotContain(Reason, HandlingLogger.AllText);
    }

    [Fact]
    public async Task A_department_without_handlers_or_inactive_is_an_invalid_target_for_every_ticket()
    {
        var id = Ticket(DevId, AlonId, TicketStatus.Assigned);

        var noHandlers = await Assign(AlonId, new BulkAssignRequest(null, SecretariatId, Reason, [Item(id)]));
        var inactive = await Assign(AlonId, new BulkAssignRequest(null, ClosedDeptId, Reason, [Item(id)]));

        Assert.Equal([new BulkFailureDto(id, BulkFailureReason.InvalidTarget, "department-has-no-handlers")],
            noHandlers.Failed);
        Assert.Equal(BulkFailureReason.InvalidTarget, Assert.Single(inactive.Failed).Reason);
        Assert.Equal((int?)DevId, Stored(id).AssignedDepartmentId);
    }

    // ===================== Validation (400 for the whole request) =====================

    [Fact]
    public async Task More_than_100_tickets_is_rejected()
    {
        var items = Enumerable.Range(1, BulkLimits.MaxItems + 1).Select(Item).ToArray();

        var error = await Assert.ThrowsAsync<ValidationException>(() => Start(AlonId, items));

        Assert.Equal([BulkItemRules.TooManyMessage], error.Errors["items"]);
    }

    [Fact]
    public async Task An_empty_list_is_rejected()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => Start(AlonId));

        Assert.Equal([BulkItemRules.ItemsRequiredMessage], error.Errors["items"]);
    }

    [Fact]
    public async Task A_repeated_ticket_is_rejected_and_nothing_changes()
    {
        var id = Ticket(DevId, AlonId, TicketStatus.Assigned);

        var error = await Assert.ThrowsAsync<ValidationException>(() => Start(AlonId, Item(id), Item(id)));

        Assert.Equal([BulkItemRules.DuplicateMessage(id)], error.Errors["items"]);
        Assert.Equal(TicketStatus.Assigned, Stored(id).Status);
    }

    [Fact]
    public async Task An_item_without_a_version_is_rejected_by_its_index()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            Start(AlonId, Item(1), new BulkTicketItem(2, null)));

        Assert.Equal([HandlingRules.RowVersionMessage], error.Errors["items[1].rowVersion"]);
    }

    [Theory]
    [InlineData(TicketStatus.WaitingForEmployee)]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task A_status_other_than_start_is_rejected(TicketStatus status)
    {
        var id = Ticket(DevId, AlonId, TicketStatus.InProgress);

        var error = await Assert.ThrowsAsync<ValidationException>(() => BulkFor(AlonId).ChangeStatusAsync(
            new BulkChangeStatusRequest(status, [Item(id)]), ClientIp, CancellationToken.None));

        Assert.Equal([BulkChangeStatusRequestValidator.StartOnlyMessage], error.Errors["newStatus"]);
        Assert.Equal(TicketStatus.InProgress, Stored(id).Status);
    }

    [Fact]
    public async Task A_transfer_needs_one_target_and_a_reason()
    {
        var noReason = await Assert.ThrowsAsync<ValidationException>(() =>
            Assign(AlonId, new BulkAssignRequest(DanaId, null, "  ", [Item(1)])));
        var twoTargets = await Assert.ThrowsAsync<ValidationException>(() =>
            Assign(AlonId, new BulkAssignRequest(DanaId, HrId, Reason, [Item(1)])));

        Assert.Equal([HandlingRules.ReasonRequiredMessage], noReason.Errors["reason"]);
        Assert.Equal([AssignTicketRequestValidator.OneTargetMessage], twoTargets.Errors["targetUserId"]);
    }

    // ===================== Failure reasons =====================

    [Theory]
    [InlineData("assignee-not-handler", BulkFailureReason.InvalidTarget)]
    [InlineData("assignee-is-requester", BulkFailureReason.InvalidTarget)]
    [InlineData("assignment-unchanged", BulkFailureReason.InvalidTarget)]
    [InlineData("ticket-not-routed", BulkFailureReason.InvalidTarget)]
    [InlineData("department-has-no-handlers", BulkFailureReason.InvalidTarget)]
    [InlineData("invalid-status-transition", BulkFailureReason.InvalidTransition)]
    [InlineData("ticket-assigned-to-other", BulkFailureReason.InvalidTransition)]
    [InlineData("ticket-not-assigned", BulkFailureReason.InvalidTransition)]
    [InlineData("ticket-not-open", BulkFailureReason.InvalidTransition)]
    public void Business_rules_are_a_transition_or_a_target(string errorType, BulkFailureReason expected)
    {
        var failure = BulkFailures.Classify(5, new BusinessRuleException(errorType: errorType));

        Assert.Equal(new BulkFailureDto(5, expected, errorType), failure);
    }

    [Fact]
    public void Not_found_and_forbidden_look_the_same_and_unexpected_errors_are_not_classified()
    {
        Assert.Equal(BulkFailureReason.NotFound, BulkFailures.Classify(1, new NotFoundException())!.Reason);
        Assert.Equal(new BulkFailureDto(1, BulkFailureReason.NotFound, null),
            BulkFailures.Classify(1, new ForbiddenException(errorType: "status-change-not-allowed")));
        Assert.Null(BulkFailures.Classify(1, new ConflictException(errorType: "unique")));
        Assert.Null(BulkFailures.Classify(1, new InvalidOperationException()));
    }

    // ===================== Bulk transfer options =====================

    [Fact]
    public async Task The_options_are_the_departments_handlers_without_the_user_and_departments_with_handlers()
    {
        var options = await HandlingFor(AlonId).GetBulkAssignOptionsAsync(CancellationToken.None);

        Assert.Equal([DanaId], options.Handlers.Select(h => h.Id));
        Assert.Equal([HrId], options.Departments.Select(d => d.Id));
    }

    [Fact]
    public async Task Only_a_handler_gets_the_options()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            HandlingFor(RonId).GetBulkAssignOptionsAsync(CancellationToken.None));
    }

    // ===================== Helpers =====================

    private Task<BulkUpdateResultDto> Start(int userId, params BulkTicketItem[] items) =>
        BulkFor(userId).ChangeStatusAsync(new BulkChangeStatusRequest(TicketStatus.InProgress, items), ClientIp,
            CancellationToken.None);

    private Task<BulkUpdateResultDto> Assign(int userId, BulkAssignRequest request) =>
        BulkFor(userId).AssignAsync(request, ClientIp, CancellationToken.None);

    private static BulkTicketItem Item(int id) => new(id, Version);

    private static BulkTicketItem Item(int id, byte[] version) => new(id, version);

    private int Ticket(int department, int? handler, TicketStatus status, int categoryId = PayrollId,
        int requester = NoaId) =>
        AddTicket(requester, status, Now.AddDays(-1), configure: t =>
        {
            t.CategoryId = categoryId;
            t.Urgency = Urgency.Medium;
            t.ClassificationSource = ClassificationSource.Manual;
            t.AssignedDepartmentId = department;
            t.AssignedToUserId = handler;
            t.DueAt = t.CreatedAt.AddHours(SlaDefaults.Hours[Urgency.Medium]);
            t.ResolvedAt = status == TicketStatus.Resolved ? Now.AddDays(-1) : null;
            t.RowVersion = Version;
        });

    private Ticket Stored(int id) => AllTickets().Single(t => t.Id == id);

    private List<TicketChange> Changes(int id)
    {
        using var db = CreateContext(signedInUserId: null);
        return db.TicketChanges.Where(c => c.TicketId == id).OrderBy(c => c.Id).ToList();
    }

    /// <summary>The first save fails as a concurrent update would; the ticket stays modified in the context.</summary>
    private sealed class ConflictOnFirstSave(IUnitOfWork inner) : IUnitOfWork
    {
        private bool _failed;

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (_failed)
            {
                return inner.SaveChangesAsync(cancellationToken);
            }

            _failed = true;
            return Task.FromException(new ConflictException(errorType: "concurrency"));
        }

        public void DiscardChanges() => inner.DiscardChanges();

        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action,
            CancellationToken cancellationToken) => inner.ExecuteInTransactionAsync(action, cancellationToken);
    }
}
