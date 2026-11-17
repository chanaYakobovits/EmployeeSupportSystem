using Common.DTOs.Dashboard;
using Common.Enums;
using Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Repositories.Data;
using Repositories.Data.Interceptors;
using Repositories.Implementations;
using Repositories.Interfaces;
using Services.Implementations;
using Services.Implementations.Dashboard;
using Services.Tests.Auth.Fakes;
using Services.Tests.Fakes;
using Services.Tests.Tickets;
using Services.Validators;

namespace Services.Tests.Dashboard;

/// <summary>
/// The dashboard service (task 5.1) over InMemory: permissions (from the database), the range, the cache and its
/// invalidation. The metrics themselves need SQL Server (DATEDIFF, AT TIME ZONE) – <see cref="DashboardRepositorySqlTests"/>;
/// here the repository is a fake that records its calls.
/// Organization A of <see cref="TicketTestBase"/>: Ron manages Development, Rina manages HR, Avner is the executive.
/// Now = 12/11/2026 11:00 Israel time.
/// </summary>
public sealed class DashboardServiceTests : TicketTestBase
{
    private readonly FakeDashboardRepository _repository = new();
    private readonly MemoryDashboardCache _cache = new(new MemoryCache(new MemoryCacheOptions()));
    private readonly TicketDataVersions _versions = new();

    private DashboardService DashboardFor(int userId, Role role)
    {
        var db = CreateContext(userId);
        var currentUser = new FakeCurrentUser { SignedInUserId = userId, SignedInOrganizationId = OrgA, SignedInRoles = [role] };
        return new DashboardService(_repository, new UserRepository(db), new DepartmentRepository(db),
            new CategoryRepository(db), ReferenceData, _cache, _versions, currentUser, Clock,
            new DepartmentDashboardQueryValidator(), new OrganizationDashboardQueryValidator(),
            new CapturingLogger<DashboardService>());
    }

    private static DepartmentDashboardQuery Department(DashboardView? view = null, DateOnly? from = null, DateOnly? to = null) =>
        new() { View = view, From = from, To = to };

    // ===================== Permissions =====================

    [Fact]
    public async Task Department_manager_sees_their_department_in_both_views()
    {
        var requesters = await DashboardFor(RonId, Role.DepartmentManager).GetDepartmentAsync(DevId, Department(), default);
        var handling = await DashboardFor(RonId, Role.DepartmentManager)
            .GetDepartmentAsync(DevId, Department(DashboardView.Handling), default);

        Assert.Equal(DashboardView.Requesters, requesters.View);
        Assert.Equal(new DashboardDepartmentDto(DevId, "פיתוח", true), requesters.Department);
        Assert.Equal(DashboardView.Handling, handling.View);
        Assert.Equal(
            [new DashboardScope(DevId, DashboardView.Requesters), new DashboardScope(DevId, DashboardView.Handling)],
            _repository.Scopes.Distinct());
    }

    [Fact]
    public async Task Department_manager_of_another_department_gets_not_found()
    {
        var error = await Assert.ThrowsAsync<NotFoundException>(() =>
            DashboardFor(RonId, Role.DepartmentManager).GetDepartmentAsync(HrId, Department(), default));

        Assert.Equal(DashboardService.DepartmentNotFoundTitle, error.Title);
        Assert.Empty(_repository.Scopes);
    }

    [Fact]
    public async Task Executive_sees_any_department()
    {
        var dev = await DashboardFor(AvnerId, Role.Executive).GetDepartmentAsync(DevId, Department(), default);
        var hr = await DashboardFor(AvnerId, Role.Executive)
            .GetDepartmentAsync(HrId, Department(DashboardView.Handling), default);

        Assert.Equal(DevId, dev.Department.Id);
        Assert.Equal(HrId, hr.Department.Id);
    }

    [Fact]
    public async Task A_department_outside_the_organization_is_not_found_even_for_the_executive()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            DashboardFor(AvnerId, Role.Executive).GetDepartmentAsync(OrgBDeptId, Department(), default));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            DashboardFor(AvnerId, Role.Executive).GetDepartmentAsync(999, Department(), default));
    }

    [Theory]
    [InlineData(NoaId)]   // employee
    [InlineData(AlonId)]  // handler
    public async Task Employee_and_handler_are_forbidden(int userId)
    {
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            DashboardFor(userId, Role.Employee).GetDepartmentAsync(DevId, Department(), default));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            DashboardFor(userId, Role.Employee).GetOrganizationAsync(new OrganizationDashboardQuery(), default));
    }

    [Fact]
    public async Task Roles_are_read_from_the_database_not_from_the_token()
    {
        // Noa's token claims Executive; the database says employee.
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            DashboardFor(NoaId, Role.Executive).GetOrganizationAsync(new OrganizationDashboardQuery(), default));
    }

    [Fact]
    public async Task Organization_dashboard_is_for_the_executive_only()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            DashboardFor(RonId, Role.DepartmentManager).GetOrganizationAsync(new OrganizationDashboardQuery(), default));

        var dashboard = await DashboardFor(AvnerId, Role.Executive).GetOrganizationAsync(new OrganizationDashboardQuery(), default);

        Assert.Equal(DashboardScope.Organization, Assert.Single(_repository.Scopes.Distinct()));
        Assert.Equal(1, _repository.ComparisonCalls);
        // Every active department, zeros included; no "not routed" row without such tickets.
        Assert.Equal(["משאבי אנוש", "פיתוח"], dashboard.Departments.Select(d => d.DepartmentName));
    }

    // ===================== Range =====================

    [Fact]
    public async Task Default_range_is_the_last_30_days_up_to_today_in_Israel()
    {
        var dashboard = await DashboardFor(RonId, Role.DepartmentManager).GetDepartmentAsync(DevId, Department(), default);

        Assert.Equal(new DashboardRangeDto(new DateOnly(2026, 10, 14), new DateOnly(2026, 11, 12)), dashboard.Range);
        Assert.Equal(30, dashboard.Trend.Count);
        var window = _repository.Windows[0];
        // 14/10 00:00 Israel = 13/10 21:00 UTC (summer time); 13/11 00:00 Israel = 12/11 22:00 UTC (winter time).
        Assert.Equal(new DateTime(2026, 10, 13, 21, 0, 0, DateTimeKind.Utc), window.FromUtc);
        Assert.Equal(new DateTime(2026, 11, 12, 22, 0, 0, DateTimeKind.Utc), window.ToUtc);
        // This week: 6/11 – 12/11 (Israel).
        Assert.Equal(new DateTime(2026, 11, 5, 22, 0, 0, DateTimeKind.Utc), window.WeekStartUtc);
        Assert.Equal(Now, window.NowUtc);
    }

    [Fact]
    public async Task One_day_given_derives_the_other_30_days_away()
    {
        var fromOnly = await DashboardFor(RonId, Role.DepartmentManager)
            .GetDepartmentAsync(DevId, Department(from: new DateOnly(2026, 1, 1)), default);
        var toOnly = await DashboardFor(RonId, Role.DepartmentManager)
            .GetDepartmentAsync(DevId, Department(to: new DateOnly(2026, 3, 31)), default);

        Assert.Equal(new DashboardRangeDto(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 30)), fromOnly.Range);
        Assert.Equal(new DashboardRangeDto(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 31)), toOnly.Range);
    }

    [Fact]
    public async Task From_after_to_is_invalid()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => DashboardFor(RonId, Role.DepartmentManager)
            .GetDepartmentAsync(DevId, Department(from: new DateOnly(2026, 11, 2), to: new DateOnly(2026, 11, 1)), default));

        Assert.Equal([DepartmentDashboardQueryValidator.FromAfterToMessage], error.Errors!["from"]);
        Assert.False(error.Errors.ContainsKey("to"));
        Assert.Empty(_repository.Scopes);
    }

    [Fact]
    public async Task Range_is_at_most_366_days()
    {
        var from = new DateOnly(2025, 11, 12);
        var ok = await DashboardFor(AvnerId, Role.Executive)
            .GetOrganizationAsync(new OrganizationDashboardQuery { From = from, To = from.AddDays(365) }, default);
        var error = await Assert.ThrowsAsync<ValidationException>(() => DashboardFor(AvnerId, Role.Executive)
            .GetOrganizationAsync(new OrganizationDashboardQuery { From = from, To = from.AddDays(366) }, default));

        Assert.Equal(366, ok.Trend.Count);
        Assert.Equal([OrganizationDashboardQueryValidator.RangeTooLongMessage], error.Errors!["to"]);
    }

    [Fact]
    public async Task Unknown_view_is_invalid()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => DashboardFor(RonId, Role.DepartmentManager)
            .GetDepartmentAsync(DevId, Department((DashboardView)7), default));

        Assert.True(error.Errors!.ContainsKey("view"));
    }

    [Fact]
    public async Task Validation_comes_before_permissions()
    {
        // 400 ← 403/404: an invalid range of another department is still a 400.
        await Assert.ThrowsAsync<ValidationException>(() => DashboardFor(RonId, Role.DepartmentManager)
            .GetDepartmentAsync(HrId, Department(from: new DateOnly(2026, 11, 2), to: new DateOnly(2026, 11, 1)), default));
    }

    // ===================== No tickets =====================

    [Fact]
    public async Task Without_tickets_counts_are_zero_and_ratios_are_null()
    {
        var dashboard = await DashboardFor(AvnerId, Role.Executive).GetOrganizationAsync(new OrganizationDashboardQuery(), default);

        Assert.Equal(new DashboardCardsDto(0, 0, 0, 0, 0, null, null, null), dashboard.Cards);
        Assert.Empty(dashboard.ByCategory);
        Assert.Equal(Enum.GetValues<TicketStatus>(), dashboard.ByStatus.Select(s => s.Status));
        Assert.All(dashboard.ByStatus, s => Assert.Equal(0, s.Count));
        Assert.All(dashboard.Trend, p => Assert.Equal((0, 0), (p.Opened, p.Closed)));
        Assert.Empty(dashboard.OldestOpen);
        Assert.All(dashboard.Departments, d => Assert.Equal((0, 0, 0, (double?)null),
            (d.OpenCount, d.OverdueOpenCount, d.ResolvedInRangeCount, d.AverageResolutionHours)));
    }

    // ===================== Cache =====================

    [Fact]
    public async Task Second_request_is_served_from_the_cache()
    {
        var first = await DashboardFor(RonId, Role.DepartmentManager).GetDepartmentAsync(DevId, Department(), default);
        var calls = _repository.Calls;
        var second = await DashboardFor(RonId, Role.DepartmentManager).GetDepartmentAsync(DevId, Department(), default);

        Assert.Equal(6, calls);
        Assert.Equal(calls, _repository.Calls);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task Cache_is_shared_by_the_executive_and_the_manager_of_the_same_department_and_range()
    {
        await DashboardFor(RonId, Role.DepartmentManager).GetDepartmentAsync(DevId, Department(), default);
        var calls = _repository.Calls;
        await DashboardFor(AvnerId, Role.Executive).GetDepartmentAsync(DevId, Department(DashboardView.Requesters), default);

        Assert.Equal(calls, _repository.Calls);
    }

    [Fact]
    public async Task Another_view_or_range_is_another_cache_entry()
    {
        await DashboardFor(RonId, Role.DepartmentManager).GetDepartmentAsync(DevId, Department(), default);
        await DashboardFor(RonId, Role.DepartmentManager).GetDepartmentAsync(DevId, Department(DashboardView.Handling), default);
        await DashboardFor(RonId, Role.DepartmentManager)
            .GetDepartmentAsync(DevId, Department(from: new DateOnly(2026, 11, 1), to: new DateOnly(2026, 11, 12)), default);

        Assert.Equal(18, _repository.Calls);
    }

    [Fact]
    public async Task Permission_is_checked_before_the_cache()
    {
        await DashboardFor(AvnerId, Role.Executive).GetDepartmentAsync(HrId, Department(), default);

        // Cached for the executive – still 404 for the manager of another department.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            DashboardFor(RonId, Role.DepartmentManager).GetDepartmentAsync(HrId, Department(), default));
    }

    [Fact]
    public async Task A_saved_ticket_change_invalidates_the_cached_dashboards()
    {
        var ticketId = AddTicket(NoaId, TicketStatus.New, Now.AddHours(-1));
        await DashboardFor(RonId, Role.DepartmentManager).GetDepartmentAsync(DevId, Department(), default);
        await DashboardFor(AvnerId, Role.Executive).GetOrganizationAsync(new OrganizationDashboardQuery(), default);
        var calls = _repository.Calls;

        // A request that changes a ticket, through a context with the app's interceptor.
        await using (var db = InterceptedContext(AvnerId))
        {
            var ticket = await db.Tickets.SingleAsync(t => t.Id == ticketId);
            ticket.Status = TicketStatus.Assigned;
            await db.SaveChangesAsync();
        }

        await DashboardFor(RonId, Role.DepartmentManager).GetDepartmentAsync(DevId, Department(), default);
        await DashboardFor(AvnerId, Role.Executive).GetOrganizationAsync(new OrganizationDashboardQuery(), default);

        Assert.Equal(1, _versions.Get(OrgA));
        Assert.Equal(calls + 6 + 7, _repository.Calls);
    }

    [Fact]
    public async Task A_new_ticket_bumps_its_organization_only()
    {
        await using (var db = InterceptedContext(NoaId))
        {
            db.Tickets.Add(new Common.Models.Ticket
            {
                Id = 5000, OrganizationId = OrgA, RequesterId = NoaId, RequesterDepartmentId = DevId, Description = "תיאור",
                Status = TicketStatus.New, AiStatus = AiStatus.Pending, CreatedAt = Now, UpdatedAt = Now, RowVersion = [1],
            });
            await db.SaveChangesAsync();
        }

        Assert.Equal(1, _versions.Get(OrgA));
        Assert.Equal(0, _versions.Get(OrgB));
    }

    [Fact]
    public async Task A_save_without_ticket_changes_does_not_bump()
    {
        await using (var db = InterceptedContext(AvnerId))
        {
            var department = await db.Departments.SingleAsync(d => d.Id == DevId);
            department.Name = "פיתוח תוכנה";
            await db.SaveChangesAsync();
        }

        Assert.Equal(0, _versions.Get(OrgA));
    }

    [Fact]
    public void Cache_key_has_the_shape_of_spec_11_3_with_the_version()
    {
        var service = DashboardFor(RonId, Role.DepartmentManager);
        var range = new DashboardRangeDto(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));

        Assert.Equal("dashboard:1:0:10:Handling:2026-11-01:2026-11-30", service.Key(OrgA, "10", "Handling", range));
        _versions.Bump(OrgA);
        Assert.Equal("dashboard:1:1:org:-:2026-11-01:2026-11-30", service.Key(OrgA, "org", "-", range));
    }

    private AppDbContext InterceptedContext(int userId)
    {
        var currentUser = new FakeCurrentUser { SignedInUserId = userId, SignedInOrganizationId = OrgA };
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(DatabaseName)
            .AddInterceptors(new TicketChangesInterceptor(_versions))
            .Options;
        return new AppDbContext(options, new OrganizationContext(currentUser));
    }

    /// <summary>Records each call; returns no data (the metrics are tested against SQL Server).</summary>
    private sealed class FakeDashboardRepository : IDashboardRepository
    {
        public int Calls { get; private set; }
        public int ComparisonCalls { get; private set; }
        public List<DashboardScope> Scopes { get; } = [];
        public List<DashboardWindow> Windows { get; } = [];

        private void Record(DashboardScope scope, DashboardWindow window)
        {
            Calls++;
            Scopes.Add(scope);
            Windows.Add(window);
        }

        public Task<DashboardSnapshot> GetSnapshotAsync(DashboardScope scope, DashboardWindow window, CancellationToken ct)
        {
            Record(scope, window);
            return Task.FromResult(DashboardSnapshot.Empty);
        }

        public Task<IReadOnlyList<DashboardOpenedGroup>> GetOpenedGroupsAsync(DashboardScope scope, DashboardWindow window,
            CancellationToken ct)
        {
            Record(scope, window);
            return Task.FromResult<IReadOnlyList<DashboardOpenedGroup>>([]);
        }

        public Task<DashboardResolvedSummary> GetResolvedSummaryAsync(DashboardScope scope, DashboardWindow window,
            CancellationToken ct)
        {
            Record(scope, window);
            return Task.FromResult(DashboardResolvedSummary.Empty);
        }

        public Task<IReadOnlyList<DashboardDayCount>> GetOpenedPerDayAsync(DashboardScope scope, DashboardWindow window,
            CancellationToken ct)
        {
            Record(scope, window);
            return Task.FromResult<IReadOnlyList<DashboardDayCount>>([]);
        }

        public Task<IReadOnlyList<DashboardDayCount>> GetClosedPerDayAsync(DashboardScope scope, DashboardWindow window,
            CancellationToken ct)
        {
            Record(scope, window);
            return Task.FromResult<IReadOnlyList<DashboardDayCount>>([]);
        }

        public Task<IReadOnlyList<DashboardTicketDto>> GetOldestOpenAsync(DashboardScope scope, DashboardWindow window,
            int take, CancellationToken ct)
        {
            Record(scope, window);
            return Task.FromResult<IReadOnlyList<DashboardTicketDto>>([]);
        }

        public Task<IReadOnlyList<DashboardDepartmentStats>> GetHandlingDepartmentStatsAsync(DashboardWindow window,
            CancellationToken ct)
        {
            Calls++;
            ComparisonCalls++;
            return Task.FromResult<IReadOnlyList<DashboardDepartmentStats>>([]);
        }
    }
}
