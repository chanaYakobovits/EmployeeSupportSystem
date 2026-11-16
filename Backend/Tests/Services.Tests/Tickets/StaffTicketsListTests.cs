using Common.DTOs.Paging;
using Common.DTOs.Tickets;
using Common.Enums;
using Common.Exceptions;
using Common.Models;

namespace Services.Tests.Tickets;

/// <summary>GET /api/tickets – the staff lists (spec 7.6, 9.4): scopes, filters, search, sorting and validation.</summary>
public sealed class StaffTicketsListTests : TicketTestBase
{
    // ---- scopes ----

    [Fact]
    public async Task A_handler_gets_their_tickets_their_departments_queue_and_everything_it_handles()
    {
        var mine = HandledBy(DevId, AlonId);
        var queued = HandledBy(DevId, null);
        var colleagues = HandledBy(DevId, LiatId);
        HandledBy(HrId, null);   // another department's queue

        Assert.Equal([mine], await Ids(AlonId, TicketListScope.Assigned));
        Assert.Equal([queued], await Ids(AlonId, TicketListScope.Queue));
        Assert.Equal(new[] { mine, queued, colleagues }.Order(), (await Ids(AlonId, TicketListScope.Handling)).Order());
    }

    [Fact]
    public async Task A_ticket_assigned_to_the_handler_in_another_department_is_in_their_assigned_list()
    {
        var id = HandledBy(HrId, AlonId);

        Assert.Equal([id], await Ids(AlonId, TicketListScope.Assigned));
        Assert.Empty(await Ids(AlonId, TicketListScope.Handling));
    }

    [Fact]
    public async Task The_users_own_tickets_are_not_in_their_staff_lists()
    {
        var alons = AddTicket(AlonId, TicketStatus.Assigned, Now.AddHours(-1), configure: t => t.AssignedDepartmentId = DevId);

        Assert.DoesNotContain(alons, await Ids(AlonId, TicketListScope.Queue));
        Assert.DoesNotContain(alons, await Ids(AlonId, TicketListScope.Handling));
        // The other staff do see it.
        Assert.Contains(alons, await Ids(RonId, TicketListScope.Department));
        Assert.Contains(alons, await Ids(AvnerId, TicketListScope.Department));
    }

    [Fact]
    public async Task A_department_manager_gets_their_employees_tickets_and_the_tickets_their_department_handles()
    {
        var ofEmployee = AddTicket(NoaId, TicketStatus.Assigned, Now.AddHours(-1), configure: t => t.AssignedDepartmentId = HrId);
        var handledByDev = AddTicket(LiatId, TicketStatus.Assigned, Now.AddHours(-1), configure: t =>
        {
            t.RequesterDepartmentId = HrId;
            t.AssignedDepartmentId = DevId;
        });
        var unrelated = AddTicket(LiatId, TicketStatus.Assigned, Now.AddHours(-1), configure: t =>
        {
            t.RequesterDepartmentId = HrId;
            t.AssignedDepartmentId = HrId;
        });

        var rons = await Ids(RonId, TicketListScope.Department);
        Assert.Equal(new[] { ofEmployee, handledByDev }.Order(), rons.Order());

        Assert.Contains(unrelated, await Ids(RinaId, TicketListScope.Department));
    }

    [Fact]
    public async Task The_executive_gets_every_ticket_of_the_organization_only()
    {
        var a = AddTicket(NoaId, TicketStatus.New, Now.AddHours(-1));
        var b = HandledBy(HrId, LiatId);

        var ids = await Ids(AvnerId, TicketListScope.Department);

        Assert.Equal(new[] { a, b }.Order(), ids.Order());
        Assert.DoesNotContain(OrgBTicketId, ids);
    }

    [Theory]
    [InlineData(NoaId, TicketListScope.Assigned)]      // an employee
    [InlineData(NoaId, TicketListScope.Department)]
    [InlineData(AlonId, TicketListScope.Department)]   // a handler
    [InlineData(RonId, TicketListScope.Queue)]         // a department manager
    [InlineData(AvnerId, TicketListScope.Handling)]    // the executive
    public async Task A_scope_that_is_not_the_users_is_forbidden(int userId, TicketListScope scope)
    {
        var error = await Assert.ThrowsAsync<ForbiddenException>(() => Page(userId, scope));

        Assert.Equal("ticket-scope-not-allowed", error.ErrorType);
    }

    [Fact]
    public async Task An_inactive_user_is_not_signed_in()
    {
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Page(OritId, TicketListScope.Assigned));
    }

    // ---- the row ----

    [Fact]
    public async Task A_row_has_everything_the_queue_shows()
    {
        var id = AddTicket(NoaId, TicketStatus.InProgress, Now.AddDays(-1), subject: null,
            description: new string('א', 100), configure: t =>
            {
                t.CategoryId = PayrollId;
                t.Urgency = Urgency.High;
                t.ClassificationSource = ClassificationSource.Manual;
                t.AssignedDepartmentId = DevId;
                t.AssignedToUserId = AlonId;
                t.DueAt = Now.AddHours(-1);
                t.UpdatedAt = Now.AddHours(-2);
                t.RowVersion = [7, 7];
            });

        var row = (await Page(AlonId, TicketListScope.Assigned)).Items.Single();

        Assert.Equal(new StaffTicketListItemDto(id, new string('א', 80), true, "נועה ברקוביץ", "פיתוח", "שכר ותלושים",
            Urgency.High, TicketStatus.InProgress, "אלון ביטון", "פיתוח", Now.AddHours(-1), IsOverdue: true,
            ClassificationSource.Manual, Now.AddDays(-1), Now.AddHours(-2), row.RowVersion), row);
        Assert.Equal([7, 7], row.RowVersion);
    }

    // ---- filters ----

    [Fact]
    public async Task Filters_by_statuses_and_category()
    {
        var assigned = HandledBy(DevId, null, TicketStatus.Assigned, t => t.CategoryId = PayrollId);
        HandledBy(DevId, null, TicketStatus.Assigned, t => t.CategoryId = GeneralId);
        var inProgress = HandledBy(DevId, null, TicketStatus.InProgress, t => t.CategoryId = PayrollId);
        HandledBy(DevId, null, TicketStatus.Closed, t => t.CategoryId = PayrollId);

        var ids = await Ids(AlonId, TicketListScope.Queue, q =>
        {
            q.Status = [TicketStatus.Assigned, TicketStatus.InProgress];
            q.CategoryId = PayrollId;
        });

        Assert.Equal(new[] { assigned, inProgress }.Order(), ids.Order());
    }

    [Fact]
    public async Task Filters_by_urgency_including_not_set()
    {
        var high = HandledBy(DevId, null, configure: t => t.Urgency = Urgency.High);
        var notSet = HandledBy(DevId, null, configure: t => t.Urgency = null);
        HandledBy(DevId, null, configure: t => t.Urgency = Urgency.Low);

        Assert.Equal([high], await Ids(AlonId, TicketListScope.Queue, q => q.Urgency = [UrgencyFilter.High]));
        Assert.Equal([notSet], await Ids(AlonId, TicketListScope.Queue, q => q.Urgency = [UrgencyFilter.NotSet]));
        Assert.Equal(new[] { high, notSet }.Order(),
            (await Ids(AlonId, TicketListScope.Queue, q => q.Urgency = [UrgencyFilter.NotSet, UrgencyFilter.High])).Order());
    }

    [Fact]
    public async Task The_date_range_is_by_opening_day_in_israel_both_days_included()
    {
        // 10/11/2026 in Israel (UTC+2) = 09/11 22:00 UTC up to 10/11 22:00 UTC.
        var dayStart = new DateTime(2026, 11, 9, 22, 0, 0, DateTimeKind.Utc);
        HandledBy(DevId, null, createdAt: dayStart.AddSeconds(-1));
        var first = HandledBy(DevId, null, createdAt: dayStart);
        var last = HandledBy(DevId, null, createdAt: dayStart.AddDays(1).AddSeconds(-1));
        HandledBy(DevId, null, createdAt: dayStart.AddDays(1));

        var ids = await Ids(AlonId, TicketListScope.Queue, q =>
        {
            q.From = new DateOnly(2026, 11, 10);
            q.To = new DateOnly(2026, 11, 10);
        });

        Assert.Equal(new[] { first, last }.Order(), ids.Order());
    }

    [Fact]
    public async Task Overdue_means_past_due_while_the_sla_clock_runs()
    {
        var overdue = HandledBy(DevId, null, TicketStatus.InProgress, t => t.DueAt = Now.AddMinutes(-1));
        HandledBy(DevId, null, TicketStatus.WaitingForEmployee, t => t.DueAt = Now.AddHours(-5));   // paused
        HandledBy(DevId, null, TicketStatus.Resolved, t => t.DueAt = Now.AddHours(-5));
        HandledBy(DevId, null, TicketStatus.Assigned, t => t.DueAt = Now.AddMinutes(1));
        HandledBy(DevId, null, TicketStatus.Assigned, t => t.DueAt = Now);   // due right now: not yet past

        Assert.Equal([overdue], await Ids(AlonId, TicketListScope.Queue, q => q.OverdueOnly = true));

        var flagged = (await Page(AlonId, TicketListScope.Queue)).Items.Where(r => r.IsOverdue).Select(r => r.Id);
        Assert.Equal([overdue], flagged);
    }

    [Fact]
    public async Task Searches_the_subject_and_the_requesters_name()
    {
        var bySubject = HandledBy(DevId, null, configure: t => t.Subject = "תלוש שכר חסר");
        var byNoa = HandledBy(DevId, null, configure: t => t.Subject = "מחשב");
        var byIdo = HandledBy(DevId, null, configure: t =>
        {
            t.RequesterId = IdoId;
            t.Subject = "ציוד";
        });

        Assert.Equal([bySubject], await Ids(AlonId, TicketListScope.Queue, q => q.Search = " תלוש "));
        // Every ticket of HandledBy is Noa's except byIdo.
        Assert.Equal(new[] { bySubject, byNoa }.Order(),
            (await Ids(AlonId, TicketListScope.Queue, q => q.Search = "נועה ברקוביץ")).Order());
        Assert.Equal([byIdo], await Ids(AlonId, TicketListScope.Queue, q => q.Search = "שגיא"));
    }

    // ---- sorting ----

    [Fact]
    public async Task By_due_time_the_nearest_first_then_the_most_urgent_and_no_due_time_last()
    {
        var noDueAt = HandledBy(DevId, null, configure: t => t.DueAt = null);
        var later = HandledBy(DevId, null, configure: t => t.DueAt = Now.AddHours(5));
        var soonLow = HandledBy(DevId, null, configure: t =>
        {
            t.DueAt = Now.AddHours(1);
            t.Urgency = Urgency.Low;
        });
        var soonCritical = HandledBy(DevId, null, configure: t =>
        {
            t.DueAt = Now.AddHours(1);
            t.Urgency = Urgency.Critical;
        });

        Assert.Equal([soonCritical, soonLow, later, noDueAt],
            await Ids(AlonId, TicketListScope.Queue, q => q.SortBy = StaffTicketSortFields.DueAt));
        // Descending: the latest due time first; still no due time last.
        Assert.Equal([later, soonCritical, soonLow, noDueAt], await Ids(AlonId, TicketListScope.Queue, q =>
        {
            q.SortBy = StaffTicketSortFields.DueAt;
            q.SortDir = "desc";
        }));
    }

    // ---- sortBy=priority (the default) ----

    [Fact]
    public async Task By_default_the_groups_are_waiting_for_the_handler_then_the_employee_then_resolved_then_done()
    {
        var resolved = HandledBy(DevId, null, TicketStatus.Resolved);
        var closed = HandledBy(DevId, null, TicketStatus.Closed);
        var waiting = HandledBy(DevId, null, TicketStatus.WaitingForEmployee);
        var cancelled = HandledBy(DevId, null, TicketStatus.Cancelled);
        var inProgress = HandledBy(DevId, null, TicketStatus.InProgress);
        var isNew = HandledBy(DevId, null, TicketStatus.New);

        var all = await Ids(AlonId, TicketListScope.Queue, q => q.Status = [.. Enum.GetValues<TicketStatus>()]);

        // Same urgency and due time in group 1 → the lowest id; groups 2–4 by the latest update (all equal here) → id.
        Assert.Equal([inProgress, isNew, waiting, resolved, closed, cancelled], all);
        // Without sortBy and with sortBy=priority alike; sortDir does not reverse it.
        Assert.Equal(all, await Ids(AlonId, TicketListScope.Queue, q =>
        {
            q.Status = [.. Enum.GetValues<TicketStatus>()];
            q.SortBy = StaffTicketSortFields.Priority;
            q.SortDir = "desc";
        }));
    }

    [Fact]
    public async Task By_priority_an_overdue_ticket_comes_before_a_critical_one_that_is_not()
    {
        var critical = HandledBy(DevId, null, configure: t =>
        {
            t.Urgency = Urgency.Critical;
            t.DueAt = Now.AddMinutes(10);
        });
        var overdueLow = HandledBy(DevId, null, configure: t =>
        {
            t.Urgency = Urgency.Low;
            t.DueAt = Now.AddMinutes(-1);
        });

        Assert.Equal([overdueLow, critical], await Ids(AlonId, TicketListScope.Queue));
    }

    [Fact]
    public async Task By_priority_urgency_beats_a_nearer_due_time_and_no_urgency_counts_as_medium()
    {
        var mediumSoon = HandledBy(DevId, null, configure: t =>
        {
            t.Urgency = Urgency.Medium;
            t.DueAt = Now.AddHours(1);
        });
        var low = HandledBy(DevId, null, configure: t =>
        {
            t.Urgency = Urgency.Low;
            t.DueAt = Now.AddMinutes(5);
        });
        var criticalLater = HandledBy(DevId, null, configure: t =>
        {
            t.Urgency = Urgency.Critical;
            t.DueAt = Now.AddHours(10);
        });
        var notSetSooner = HandledBy(DevId, null, configure: t =>
        {
            t.Urgency = null;
            t.DueAt = Now.AddMinutes(30);
        });
        var notSetNoDueAt = HandledBy(DevId, null, TicketStatus.New, configure: t =>
        {
            t.Urgency = null;
            t.DueAt = null;
        });

        // Medium and "not set" share a rank, then the nearest due time; no due time last within the rank.
        Assert.Equal([criticalLater, notSetSooner, mediumSoon, notSetNoDueAt, low],
            await Ids(AlonId, TicketListScope.Queue));
    }

    [Fact]
    public async Task By_priority_the_other_groups_are_by_the_latest_update()
    {
        var older = HandledBy(DevId, null, TicketStatus.WaitingForEmployee, t => t.UpdatedAt = Now.AddHours(-5));
        var newer = HandledBy(DevId, null, TicketStatus.WaitingForEmployee, t =>
        {
            t.UpdatedAt = Now.AddHours(-1);
            t.Urgency = Urgency.Low;   // urgency and due time do not count outside group 1
            t.DueAt = Now.AddDays(5);
        });
        var resolvedOld = HandledBy(DevId, null, TicketStatus.Resolved, t => t.UpdatedAt = Now.AddDays(-3));
        var resolvedNew = HandledBy(DevId, null, TicketStatus.Resolved, t => t.UpdatedAt = Now.AddDays(-2));
        var urgentOld = HandledBy(DevId, null, TicketStatus.WaitingForEmployee, t =>
        {
            t.UpdatedAt = Now.AddHours(-9);
            t.Urgency = Urgency.Critical;
            t.DueAt = Now.AddHours(-9);
        });

        Assert.Equal([newer, older, urgentOld, resolvedNew, resolvedOld], await Ids(AlonId, TicketListScope.Queue));
    }

    [Fact]
    public async Task By_priority_pages_are_stable_with_ties()
    {
        // Identical keys everywhere: only the id orders them.
        var ids = Enumerable.Range(1, 7).Select(_ => HandledBy(DevId, null)).ToList();

        var pages = new List<int>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await Page(AlonId, TicketListScope.Queue, q =>
            {
                q.PageSize = 3;
                q.Page = page;
            });
            Assert.Equal(7, result.TotalCount);
            pages.AddRange(result.Items.Select(r => r.Id));
        }

        Assert.Equal(ids.Order(), pages);
    }

    [Fact]
    public async Task Closed_and_cancelled_tickets_are_left_out_unless_the_status_filter_asks_for_them()
    {
        var open = HandledBy(DevId, null, TicketStatus.InProgress);
        var resolved = HandledBy(DevId, null, TicketStatus.Resolved);
        var closed = HandledBy(DevId, null, TicketStatus.Closed);
        var cancelled = HandledBy(DevId, null, TicketStatus.Cancelled);

        var byDefault = await Page(AlonId, TicketListScope.Handling);
        Assert.Equal([open, resolved], byDefault.Items.Select(r => r.Id));
        Assert.Equal(2, byDefault.TotalCount);

        Assert.Equal([closed], await Ids(AlonId, TicketListScope.Handling, q => q.Status = [TicketStatus.Closed]));
        Assert.Equal([open, closed, cancelled], await Ids(AlonId, TicketListScope.Handling,
            q => q.Status = [TicketStatus.InProgress, TicketStatus.Closed, TicketStatus.Cancelled]));
        // The executive's department scope too.
        Assert.DoesNotContain(closed, await Ids(AvnerId, TicketListScope.Department));
    }

    [Fact]
    public async Task By_urgency_the_most_urgent_first_and_no_urgency_counts_as_medium()
    {
        var low = HandledBy(DevId, null, configure: t => t.Urgency = Urgency.Low);
        var critical = HandledBy(DevId, null, configure: t => t.Urgency = Urgency.Critical);
        var notSet = HandledBy(DevId, null, configure: t =>
        {
            t.Urgency = null;
            t.DueAt = Now.AddHours(1);
        });
        var medium = HandledBy(DevId, null, configure: t =>
        {
            t.Urgency = Urgency.Medium;
            t.DueAt = Now.AddHours(2);
        });

        Assert.Equal([critical, notSet, medium, low],
            await Ids(AlonId, TicketListScope.Queue, q => q.SortBy = StaffTicketSortFields.Urgency));
        Assert.Equal([low, notSet, medium, critical], await Ids(AlonId, TicketListScope.Queue, q =>
        {
            q.SortBy = StaffTicketSortFields.Urgency;
            q.SortDir = "asc";
        }));
    }

    [Fact]
    public async Task By_creation_the_newest_first_and_pages_are_stable()
    {
        var ids = Enumerable.Range(1, 5).Select(i => HandledBy(DevId, null, createdAt: Now.AddHours(-i))).ToList();

        var page = await Page(AlonId, TicketListScope.Queue, q =>
        {
            q.SortBy = StaffTicketSortFields.CreatedAt;
            q.PageSize = 2;
            q.Page = 2;
        });

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(ids.Skip(2).Take(2), page.Items.Select(r => r.Id));
    }

    // ---- validation ----

    [Fact]
    public async Task The_scope_is_required()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            ServiceFor(AlonId).GetStaffPageAsync(new StaffTicketsQuery(), CancellationToken.None));

        Assert.True(error.Errors!.ContainsKey("scope"));
    }

    [Fact]
    public async Task Invalid_values_are_rejected_by_field()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => Page(AlonId, TicketListScope.Queue, q =>
        {
            q.From = new DateOnly(2026, 11, 10);
            q.To = new DateOnly(2026, 11, 9);
            q.SortBy = "subject";
            q.PageSize = 101;
            q.Search = new string('א', 101);
            q.Urgency = [(UrgencyFilter)9];
        }));

        Assert.Equal(new[] { "pageSize", "search", "sortBy", "to", "urgency[0]" }, error.Errors!.Keys.Order());
    }

    // ---- helpers ----

    /// <summary>A ticket of Noa (Development) handled by <paramref name="department"/>, assigned to <paramref name="handler"/>.</summary>
    private int HandledBy(int department, int? handler, TicketStatus status = TicketStatus.Assigned,
        Action<Ticket>? configure = null, DateTime? createdAt = null) =>
        AddTicket(NoaId, status, createdAt ?? Now.AddDays(-1), configure: t =>
        {
            t.AssignedDepartmentId = department;
            t.AssignedToUserId = handler;
            t.DueAt = Now.AddDays(1);
            configure?.Invoke(t);
        });

    private Task<PagedResult<StaffTicketListItemDto>> Page(int userId, TicketListScope scope,
        Action<StaffTicketsQuery>? configure = null)
    {
        var query = new StaffTicketsQuery { Scope = scope };
        configure?.Invoke(query);
        return ServiceFor(userId).GetStaffPageAsync(query, CancellationToken.None);
    }

    private async Task<List<int>> Ids(int userId, TicketListScope scope, Action<StaffTicketsQuery>? configure = null) =>
        (await Page(userId, scope, configure)).Items.Select(r => r.Id).ToList();
}
