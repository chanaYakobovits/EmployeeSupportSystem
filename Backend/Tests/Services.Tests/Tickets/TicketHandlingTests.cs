using System.Text.Json;
using Common.Constants;
using Common.DTOs.Tickets;
using Common.Enums;
using Common.Exceptions;
using Common.Models;

namespace Services.Tests.Tickets;

/// <summary>
/// Task 3.4: reclassification (PATCH /classification) and transfer (POST /assign) – who may, routing and SLA,
/// the status on a handler change, concurrency (spec 11.2) and the reason in the staff timeline only.
/// </summary>
public sealed class TicketHandlingTests : TicketTestBase
{
    private const int DanaId = 9;          // a second handler in Development
    private const int GilId = 30;          // an inactive handler in Development
    private const int EquipmentId = 3;     // another category of Development
    private const int ArchivedId = 4;      // an inactive category
    private const int SecretariatId = 12;  // a department without handlers
    private const int ClosedDeptId = 13;   // an inactive department
    private const string Reason = "נימוק פנימי לבדיקה";

    private static readonly byte[] Version = [0, 0, 0, 0, 0, 0, 0, 7];
    private static readonly byte[] OldVersion = [0, 0, 0, 0, 0, 0, 0, 6];

    public TicketHandlingTests()
    {
        AddUser(DanaId, DevId, "דנה", "לוי", Role.Handler);
        AddUser(GilId, DevId, "גיל", "רז", Role.Handler, isActive: false);
        AddCategory(EquipmentId, "EQUIPMENT", "ציוד", DevId);
        AddCategory(ArchivedId, "ARCHIVED", "ישנה", DevId, isActive: false);
        AddDepartment(SecretariatId, "מזכירות");
        AddDepartment(ClosedDeptId, "סגורה", isActive: false);
    }

    // ===================== Reclassification =====================

    [Fact]
    public async Task Reclassifying_to_a_category_of_another_department_routes_it_there_with_a_new_handler()
    {
        // Liat (HR) is handling a fallback ticket; it belongs to payroll (Development).
        var id = Ticket(HrId, LiatId, TicketStatus.InProgress, categoryId: GeneralId, urgency: null);

        var result = await HandlingFor(LiatId).ReclassifyAsync(id,
            new ReclassifyTicketRequest(PayrollId, Urgency.High, Reason, Version), CancellationToken.None);

        var ticket = Stored(id);
        Assert.Equal((PayrollId, Urgency.High, ClassificationSource.Manual), (ticket.CategoryId, ticket.Urgency, ticket.ClassificationSource));
        Assert.Equal((DevId, (int?)AlonId), (ticket.AssignedDepartmentId, ticket.AssignedToUserId));
        // The new handler has not started yet.
        Assert.Equal(TicketStatus.Assigned, ticket.Status);
        Assert.Contains(ticket.StatusHistory, h => h is
            { PreviousStatus: TicketStatus.InProgress, NewStatus: TicketStatus.Assigned, ChangedByUserId: LiatId });
        // Liat no longer has it in scope: the screen goes back to the list.
        Assert.False(result.CanView);

        var change = Changes(id).Single();
        Assert.Equal(TicketChangeKind.Reclassified, change.Kind);
        Assert.Equal(LiatId, change.ChangedByUserId);
        Assert.Equal(Reason, change.Reason);
        Assert.Equal((GeneralId, PayrollId), (change.FromCategoryId, change.ToCategoryId));
        Assert.Equal((HrId, DevId), (change.FromDepartmentId, change.ToDepartmentId));
        Assert.Equal((LiatId, AlonId), (change.FromUserId, change.ToUserId));
    }

    [Fact]
    public async Task A_category_of_the_same_department_keeps_the_handler_and_the_status()
    {
        var id = Ticket(DevId, DanaId, TicketStatus.InProgress, categoryId: PayrollId);

        var result = await HandlingFor(DanaId).ReclassifyAsync(id,
            new ReclassifyTicketRequest(EquipmentId, null, Reason, Version), CancellationToken.None);

        var ticket = Stored(id);
        Assert.Equal((EquipmentId, (int?)DevId, (int?)DanaId, TicketStatus.InProgress),
            (ticket.CategoryId, ticket.AssignedDepartmentId, ticket.AssignedToUserId, ticket.Status));
        Assert.True(result.CanView);
    }

    [Fact]
    public async Task A_new_urgency_recalculates_the_due_time_from_the_opening_so_raising_it_late_is_overdue()
    {
        // Opened 5 hours ago as Medium (due in 3 days); raised to Critical (4 hours) → it is already past due.
        var id = Ticket(DevId, AlonId, TicketStatus.InProgress, urgency: Urgency.Medium, createdAt: Now.AddHours(-5));

        await HandlingFor(AlonId).ReclassifyAsync(id,
            new ReclassifyTicketRequest(null, Urgency.Critical, Reason, Version), CancellationToken.None);

        var ticket = Stored(id);
        Assert.Equal(Now.AddHours(-1), ticket.DueAt);
        Assert.Equal((PayrollId, (int?)AlonId), (ticket.CategoryId, ticket.AssignedToUserId));
        var details = Assert.IsType<StaffTicketDetailsDto>(await ServiceFor(AlonId).GetAsync(id, ClientIp, CancellationToken.None));
        Assert.True(details.IsOverdue);
    }

    [Fact]
    public async Task Confirming_an_automatic_classification_as_is_makes_it_manual()
    {
        var id = Ticket(DevId, AlonId, TicketStatus.Assigned, urgency: Urgency.Low, source: ClassificationSource.Ai);

        await HandlingFor(AlonId).ReclassifyAsync(id,
            new ReclassifyTicketRequest(PayrollId, Urgency.Low, Reason, Version), CancellationToken.None);

        Assert.Equal(ClassificationSource.Manual, Stored(id).ClassificationSource);
    }

    [Fact]
    public async Task Nothing_to_change_on_a_manual_classification_is_rejected()
    {
        var id = Ticket(DevId, AlonId, TicketStatus.Assigned, urgency: Urgency.Low);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => HandlingFor(AlonId).ReclassifyAsync(id,
            new ReclassifyTicketRequest(PayrollId, Urgency.Low, Reason, Version), CancellationToken.None));

        Assert.Equal("classification-unchanged", error.ErrorType);
    }

    [Fact]
    public async Task The_manager_of_the_handling_department_reclassifies_but_not_a_ticket_handled_elsewhere()
    {
        var handledByDev = Ticket(DevId, AlonId, TicketStatus.Assigned);
        var ofDevEmployee = Ticket(HrId, LiatId, TicketStatus.Assigned, categoryId: GeneralId);

        await HandlingFor(RonId).ReclassifyAsync(handledByDev,
            new ReclassifyTicketRequest(null, Urgency.High, Reason, Version), CancellationToken.None);

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => HandlingFor(RonId).ReclassifyAsync(ofDevEmployee,
            new ReclassifyTicketRequest(null, Urgency.High, Reason, Version), CancellationToken.None));
        Assert.Equal("reclassify-not-allowed", error.ErrorType);
        Assert.Equal(Urgency.High, Stored(handledByDev).Urgency);
    }

    [Theory]
    [InlineData(AvnerId)]   // the executive only views
    [InlineData(NoaId)]     // the requester
    public async Task Users_who_only_view_cannot_reclassify(int userId)
    {
        var id = Ticket(DevId, AlonId, TicketStatus.Assigned);

        await Assert.ThrowsAsync<ForbiddenException>(() => HandlingFor(userId).ReclassifyAsync(id,
            new ReclassifyTicketRequest(null, Urgency.High, Reason, Version), CancellationToken.None));
    }

    [Fact]
    public async Task Outside_every_scope_is_not_found()
    {
        var id = Ticket(HrId, LiatId, TicketStatus.Assigned, categoryId: GeneralId, requesterDepartment: HrId,
            requester: RinaId);

        await Assert.ThrowsAsync<NotFoundException>(() => HandlingFor(AlonId).ReclassifyAsync(id,
            new ReclassifyTicketRequest(null, Urgency.High, Reason, Version), CancellationToken.None));
    }

    [Fact]
    public async Task A_stale_version_is_a_conflict_and_nothing_changes()
    {
        var id = Ticket(DevId, AlonId, TicketStatus.Assigned, urgency: Urgency.Low);

        var error = await Assert.ThrowsAsync<ConflictException>(() => HandlingFor(AlonId).ReclassifyAsync(id,
            new ReclassifyTicketRequest(null, Urgency.High, Reason, OldVersion), CancellationToken.None));

        Assert.Equal("הפנייה עודכנה על ידי משתמש אחר", error.Title);
        Assert.Equal("concurrency", error.ErrorType);
        Assert.Equal(Urgency.Low, Stored(id).Urgency);
        Assert.Empty(Changes(id));
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task A_resolved_closed_or_cancelled_ticket_is_not_reclassified(TicketStatus status)
    {
        var id = Ticket(DevId, AlonId, status);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => HandlingFor(AlonId).ReclassifyAsync(id,
            new ReclassifyTicketRequest(null, Urgency.High, Reason, Version), CancellationToken.None));

        Assert.Equal("ticket-not-open", error.ErrorType);
    }

    [Fact]
    public async Task An_inactive_category_and_a_missing_reason_are_invalid()
    {
        var id = Ticket(DevId, AlonId, TicketStatus.Assigned);

        var inactive = await Assert.ThrowsAsync<ValidationException>(() => HandlingFor(AlonId).ReclassifyAsync(id,
            new ReclassifyTicketRequest(ArchivedId, null, Reason, Version), CancellationToken.None));
        var invalid = await Assert.ThrowsAsync<ValidationException>(() => HandlingFor(AlonId).ReclassifyAsync(id,
            new ReclassifyTicketRequest(null, null, "  ", [1]), CancellationToken.None));

        Assert.Equal(["categoryId"], inactive.Errors.Keys);
        Assert.Equal(new[] { "categoryId", "reason", "rowVersion" }, invalid.Errors.Keys.Order());
    }

    // ===================== Assignment =====================

    [Fact]
    public async Task A_handler_takes_a_ticket_from_the_queue_without_a_reason()
    {
        var id = Ticket(DevId, null, TicketStatus.Assigned);

        var result = await HandlingFor(AlonId).AssignAsync(id, new AssignTicketRequest(AlonId, null, null, Version),
            CancellationToken.None);

        var ticket = Stored(id);
        Assert.Equal((AlonId, TicketStatus.Assigned), (ticket.AssignedToUserId!.Value, ticket.Status));
        Assert.Equal(Now, ticket.UpdatedAt);
        Assert.True(result.CanView);
        var change = Changes(id).Single();
        Assert.Equal((TicketChangeKind.Taken, (int?)null, (int?)AlonId, (string?)null),
            (change.Kind, change.FromUserId, change.ToUserId, change.Reason));
    }

    [Fact]
    public async Task Transferring_to_a_colleague_needs_a_reason_and_puts_an_in_progress_ticket_back_to_assigned()
    {
        var id = Ticket(DevId, AlonId, TicketStatus.InProgress);

        var missing = await Assert.ThrowsAsync<ValidationException>(() => HandlingFor(AlonId).AssignAsync(id,
            new AssignTicketRequest(DanaId, null, null, Version), CancellationToken.None));
        Assert.Equal(["reason"], missing.Errors.Keys);

        await HandlingFor(AlonId).AssignAsync(id, new AssignTicketRequest(DanaId, null, Reason, Version),
            CancellationToken.None);

        var ticket = Stored(id);
        Assert.Equal((DanaId, TicketStatus.Assigned), (ticket.AssignedToUserId!.Value, ticket.Status));
        var change = Changes(id).Single();
        Assert.Equal((TicketChangeKind.Transferred, (int?)AlonId, (int?)DanaId, Reason),
            (change.Kind, change.FromUserId, change.ToUserId, change.Reason));
    }

    [Fact]
    public async Task A_ticket_waiting_for_the_employee_keeps_its_status_when_transferred()
    {
        var id = Ticket(DevId, AlonId, TicketStatus.WaitingForEmployee);

        await HandlingFor(AlonId).AssignAsync(id, new AssignTicketRequest(DanaId, null, Reason, Version),
            CancellationToken.None);

        Assert.Equal(TicketStatus.WaitingForEmployee, Stored(id).Status);
    }

    [Fact]
    public async Task The_target_cannot_be_the_requester_even_when_they_are_a_handler_of_the_department()
    {
        var id = Ticket(DevId, AlonId, TicketStatus.Assigned, requester: DanaId);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => HandlingFor(AlonId).AssignAsync(id,
            new AssignTicketRequest(DanaId, null, Reason, Version), CancellationToken.None));

        Assert.Equal("assignee-is-requester", error.ErrorType);
    }

    [Theory]
    [InlineData(LiatId)]   // a handler of another department
    [InlineData(GilId)]    // an inactive handler
    [InlineData(IdoId)]    // not a handler
    [InlineData(OrgBUserId)]   // another organization
    public async Task The_target_must_be_an_active_handler_of_the_handling_department(int targetUserId)
    {
        var id = Ticket(DevId, AlonId, TicketStatus.Assigned);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => HandlingFor(AlonId).AssignAsync(id,
            new AssignTicketRequest(targetUserId, null, Reason, Version), CancellationToken.None));

        Assert.Equal("assignee-not-handler", error.ErrorType);
        Assert.Equal(AlonId, Stored(id).AssignedToUserId);
    }

    [Fact]
    public async Task Assigning_to_the_current_handler_is_rejected()
    {
        var id = Ticket(DevId, AlonId, TicketStatus.Assigned);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => HandlingFor(AlonId).AssignAsync(id,
            new AssignTicketRequest(AlonId, null, Reason, Version), CancellationToken.None));

        Assert.Equal("assignment-unchanged", error.ErrorType);
    }

    [Fact]
    public async Task A_handler_moves_a_ticket_to_another_departments_queue()
    {
        var id = Ticket(DevId, AlonId, TicketStatus.InProgress);

        var result = await HandlingFor(AlonId).AssignAsync(id, new AssignTicketRequest(null, HrId, Reason, Version),
            CancellationToken.None);

        var ticket = Stored(id);
        Assert.Equal((HrId, (int?)null, TicketStatus.Assigned, PayrollId),
            (ticket.AssignedDepartmentId!.Value, ticket.AssignedToUserId, ticket.Status, ticket.CategoryId!.Value));
        Assert.False(result.CanView);
        var change = Changes(id).Single();
        Assert.Equal((DevId, HrId, AlonId, (int?)null),
            (change.FromDepartmentId!.Value, change.ToDepartmentId!.Value, change.FromUserId!.Value, change.ToUserId));
    }

    [Fact]
    public async Task A_department_must_be_active_have_handlers_and_differ_from_the_current_one()
    {
        var id = Ticket(DevId, AlonId, TicketStatus.Assigned);
        var handling = HandlingFor(AlonId);

        var noHandlers = await Assert.ThrowsAsync<BusinessRuleException>(() => handling.AssignAsync(id,
            new AssignTicketRequest(null, SecretariatId, Reason, Version), CancellationToken.None));
        var inactive = await Assert.ThrowsAsync<ValidationException>(() => HandlingFor(AlonId).AssignAsync(id,
            new AssignTicketRequest(null, ClosedDeptId, Reason, Version), CancellationToken.None));
        var same = await Assert.ThrowsAsync<BusinessRuleException>(() => HandlingFor(AlonId).AssignAsync(id,
            new AssignTicketRequest(null, DevId, Reason, Version), CancellationToken.None));

        Assert.Equal("department-has-no-handlers", noHandlers.ErrorType);
        Assert.Equal(["targetDepartmentId"], inactive.Errors.Keys);
        Assert.Equal("assignment-unchanged", same.ErrorType);
    }

    [Fact]
    public async Task A_department_manager_assigns_within_the_department_only()
    {
        var id = Ticket(DevId, AlonId, TicketStatus.Assigned);

        var toDepartment = await Assert.ThrowsAsync<ForbiddenException>(() => HandlingFor(RonId).AssignAsync(id,
            new AssignTicketRequest(null, HrId, Reason, Version), CancellationToken.None));
        var toOtherHandler = await Assert.ThrowsAsync<BusinessRuleException>(() => HandlingFor(RonId).AssignAsync(id,
            new AssignTicketRequest(LiatId, null, Reason, Version), CancellationToken.None));
        Assert.Equal("transfer-not-allowed", toDepartment.ErrorType);
        Assert.Equal("assignee-not-handler", toOtherHandler.ErrorType);

        await HandlingFor(RonId).AssignAsync(id, new AssignTicketRequest(DanaId, null, Reason, Version),
            CancellationToken.None);
        Assert.Equal(DanaId, Stored(id).AssignedToUserId);
    }

    [Fact]
    public async Task The_executive_and_the_requester_cannot_assign()
    {
        var id = Ticket(DevId, AlonId, TicketStatus.Assigned);

        await Assert.ThrowsAsync<ForbiddenException>(() => HandlingFor(AvnerId).AssignAsync(id,
            new AssignTicketRequest(DanaId, null, Reason, Version), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => HandlingFor(NoaId).AssignAsync(id,
            new AssignTicketRequest(null, HrId, Reason, Version), CancellationToken.None));
    }

    [Fact]
    public async Task A_stale_version_on_assign_is_a_conflict()
    {
        var id = Ticket(DevId, null, TicketStatus.Assigned);

        await Assert.ThrowsAsync<ConflictException>(() => HandlingFor(AlonId).AssignAsync(id,
            new AssignTicketRequest(AlonId, null, null, OldVersion), CancellationToken.None));

        Assert.Null(Stored(id).AssignedToUserId);
    }

    [Fact]
    public async Task Exactly_one_target_is_required()
    {
        var id = Ticket(DevId, AlonId, TicketStatus.Assigned);

        var both = await Assert.ThrowsAsync<ValidationException>(() => HandlingFor(AlonId).AssignAsync(id,
            new AssignTicketRequest(DanaId, HrId, Reason, Version), CancellationToken.None));
        var none = await Assert.ThrowsAsync<ValidationException>(() => HandlingFor(AlonId).AssignAsync(id,
            new AssignTicketRequest(null, null, Reason, Version), CancellationToken.None));

        Assert.Equal(["targetUserId"], both.Errors.Keys);
        Assert.Equal(["targetUserId"], none.Errors.Keys);
    }

    // ===================== The reason in the timeline =====================

    [Fact]
    public async Task The_reason_is_in_the_staff_timeline_and_never_reaches_the_requester_or_the_log()
    {
        var id = Ticket(DevId, AlonId, TicketStatus.InProgress, urgency: Urgency.Low);
        await HandlingFor(AlonId).ReclassifyAsync(id,
            new ReclassifyTicketRequest(EquipmentId, Urgency.High, Reason, Version), CancellationToken.None);
        await HandlingFor(AlonId).AssignAsync(id, new AssignTicketRequest(DanaId, null, Reason, Version),
            CancellationToken.None);

        var staff = Assert.IsType<StaffTicketHistoryDto>(await ConversationFor(DanaId).GetHistoryAsync(id, CancellationToken.None));
        var changes = staff.Items.OfType<StaffTimelineChangeDto>().ToList();
        Assert.Equal([TicketChangeKind.Reclassified, TicketChangeKind.Transferred], changes.Select(c => c.ChangeKind));
        Assert.Equal(("שכר ותלושים", "ציוד", Urgency.Low, Urgency.High),
            (changes[0].FromCategoryName, changes[0].ToCategoryName, changes[0].FromUrgency, changes[0].ToUrgency));
        Assert.Equal(("אלון ביטון", "דנה לוי", Reason), (changes[1].FromHandlerName, changes[1].ToHandlerName, changes[1].Reason));
        Assert.Contains("\"kind\":\"change\"", JsonSerializer.Serialize<object>(staff, ApiJson));

        var requesterHistory = JsonSerializer.Serialize<object>(
            await ConversationFor(NoaId).GetHistoryAsync(id, CancellationToken.None), ApiJson);
        var requesterDetails = JsonSerializer.Serialize<object>(
            await ServiceFor(NoaId).GetAsync(id, ClientIp, CancellationToken.None), ApiJson);
        foreach (var json in new[] { requesterHistory, requesterDetails })
        {
            Assert.DoesNotContain(Reason, json);
            Assert.DoesNotContain("\"change\"", json);
            Assert.DoesNotContain("reason", json, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(Reason, HandlingLogger.AllText);
        Assert.Contains($"Ticket {id} reclassified by user {AlonId}", HandlingLogger.AllText);
    }

    // ===================== Assign options =====================

    [Fact]
    public async Task The_options_are_the_other_active_handlers_and_the_departments_with_handlers()
    {
        var id = Ticket(DevId, AlonId, TicketStatus.InProgress);
        Ticket(DevId, DanaId, TicketStatus.Assigned);

        var options = await HandlingFor(AlonId).GetAssignOptionsAsync(id, CancellationToken.None);

        // Not Alon (the current handler), not Gil (inactive); Dana with her one open ticket.
        Assert.Equal([new AssigneeOptionDto(DanaId, "דנה לוי", 1)], options.Handlers);
        // HR has Liat; the secretariat has no handlers, the closed department is inactive, Development is the current one.
        Assert.Equal([new NamedRefDto(HrId, "משאבי אנוש")], options.Departments);
    }

    [Fact]
    public async Task The_requester_is_not_an_option_even_when_they_are_a_handler()
    {
        var id = Ticket(DevId, null, TicketStatus.Assigned, requester: DanaId);

        var options = await HandlingFor(AlonId).GetAssignOptionsAsync(id, CancellationToken.None);

        Assert.Equal([AlonId], options.Handlers.Select(h => h.Id));
    }

    [Fact]
    public async Task A_department_manager_gets_the_handlers_but_no_other_departments()
    {
        var id = Ticket(DevId, AlonId, TicketStatus.InProgress);

        var options = await HandlingFor(RonId).GetAssignOptionsAsync(id, CancellationToken.None);

        Assert.Equal([DanaId], options.Handlers.Select(h => h.Id));
        Assert.Empty(options.Departments);
    }

    [Fact]
    public async Task The_executive_may_not_ask_for_the_options()
    {
        var id = Ticket(DevId, AlonId, TicketStatus.InProgress);

        var error = await Assert.ThrowsAsync<ForbiddenException>(() =>
            HandlingFor(AvnerId).GetAssignOptionsAsync(id, CancellationToken.None));
        Assert.Equal("assign-not-allowed", error.ErrorType);
    }

    [Fact]
    public async Task The_options_of_a_ticket_outside_the_scope_are_not_found()
    {
        var id = Ticket(DevId, AlonId, TicketStatus.InProgress);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            HandlingFor(LiatId).GetAssignOptionsAsync(id, CancellationToken.None));
    }

    // ===================== helpers =====================

    /// <summary>A ticket (by default Noa's, PAYROLL, Medium) handled by <paramref name="department"/>.</summary>
    private int Ticket(int department, int? handler, TicketStatus status, int categoryId = PayrollId,
        Urgency? urgency = Urgency.Medium, ClassificationSource source = ClassificationSource.Manual,
        DateTime? createdAt = null, int requester = NoaId, int requesterDepartment = DevId) =>
        AddTicket(requester, status, createdAt ?? Now.AddDays(-1), configure: t =>
        {
            t.RequesterDepartmentId = requesterDepartment;
            t.CategoryId = categoryId;
            t.Urgency = urgency;
            t.ClassificationSource = source;
            t.AssignedDepartmentId = department;
            t.AssignedToUserId = handler;
            t.DueAt = t.CreatedAt.AddHours(SlaDefaults.Hours[urgency ?? Urgency.Medium]);
            t.RowVersion = Version;
        });

    private Ticket Stored(int id) => AllTickets().Single(t => t.Id == id);

    private List<TicketChange> Changes(int id)
    {
        using var db = CreateContext(signedInUserId: null);
        return db.TicketChanges.Where(c => c.TicketId == id).OrderBy(c => c.Id).ToList();
    }
}
