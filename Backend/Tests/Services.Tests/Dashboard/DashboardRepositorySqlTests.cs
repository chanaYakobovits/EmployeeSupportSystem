using Common.DTOs.Dashboard;
using Common.Enums;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Repositories.Data.Interceptors;
using Repositories.Implementations;
using Services.Implementations;
using Services.Implementations.Dashboard;
using Services.Tests.Auth.Fakes;
using Services.Tests.Data;
using Services.Tests.Fakes;
using Services.Validators;

namespace Services.Tests.Dashboard;

/// <summary>
/// The dashboard metrics (task 5.1, spec 7.9) against SQL Server – they use DATEDIFF and AT TIME ZONE, which InMemory
/// cannot translate – through the real service and repositories. Skipped without <see cref="SqlServerDatabase.ConnectionStringVariable"/>.
/// Each test seeds an organization of its own. Now = 12/11/2026 09:00 UTC (11:00 in Israel, UTC+2).
/// <para>
/// The known set (<see cref="SeedScenario"/>), range 1/11–12/11 (Israel) = 31/10 22:00 – 12/11 22:00 UTC:
/// T1 New (no category, not routed) · T2 Assigned, overdue, AI · T3 WaitingForEmployee past its DueAt (paused – not
/// overdue) · T4 Resolved after 24h, in time only thanks to a 24h pause · T5 Closed, urgency null (fallback), resolved
/// after 12h · T6 reopened and resolved again, 48h from the opening to the last resolution · T7 reopened, in progress ·
/// T8 Cancelled · T9 opened before the range, in progress, overdue · T10 opened before the range, resolved in it after
/// 298h, late, closed in it · T11 HR, opened at 01:30 in Israel (23:30 UTC the day before) · T12 HR's employee, handled
/// by Development · T13 Cancelled, opened at 00:30 on the range's first day in Israel (22:30 UTC the day before).
/// </para>
/// </summary>
public sealed class DashboardRepositorySqlTests(SqlServerDatabase database) : IClassFixture<SqlServerDatabase>
{
    private static readonly DateTime Now = new(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly From = new(2026, 11, 1);
    private static readonly DateOnly To = new(2026, 11, 12);

    private const string Payroll = "שכר ותלושים";
    private const string General = "כללי";

    private static DateTime Utc(int month, int day, int hour, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, DateTimeKind.Utc);

    // ===================== The organization dashboard =====================

    [SqlServerFact]
    public async Task Organization_cards_follow_the_definitions()
    {
        var s = await SeedScenario();

        var dashboard = await Organization(s);

        Assert.Equal(new DashboardRangeDto(From, To), dashboard.Range);
        Assert.Equal(new DashboardCardsDto(
            OpenCount: 7,               // T1 T2 T3 T7 T9 T11 T12 – not Resolved, Closed, Cancelled
            NewThisWeekCount: 6,        // since 6/11 00:00 Israel: T1 T2 T6 T8 T11 T12 (T3 on 5/11)
            OverdueOpenCount: 2,        // T2 T9 – T3 is past its DueAt but paused
            OpenedInRangeCount: 11,     // T1–T8, T11–T13 (T13 at 00:30 on 1/11 in Israel)
            ResolvedInRangeCount: 4,    // T4 T5 T6 T10 – T7 was reopened (no ResolvedAt), T8 cancelled
            AverageResolutionHours: 95.5, // (24 + 12 + 48 + 298) / 4
            SlaCompliancePercent: 75.0,   // T4 (pause), T5, T6 (new clock) in time; T10 late
            AutoClassifiedPercent: 36.4), // T2 T4 T7 T12 of 11
            dashboard.Cards);
    }

    [SqlServerFact]
    public async Task By_category_and_by_status_count_the_tickets_opened_in_the_range()
    {
        var s = await SeedScenario();

        var dashboard = await Organization(s);

        Assert.Equal(
            [
                new DashboardCategoryCountDto(s.GeneralId, General, 4),  // T3 T5 T7 T8
                new DashboardCategoryCountDto(s.PayrollId, Payroll, 4),  // T2 T4 T6 T12
                new DashboardCategoryCountDto(null, null, 3),            // T1 T11 T13
            ],
            dashboard.ByCategory);
        Assert.Equal(new Dictionary<TicketStatus, int>
            {
                [TicketStatus.New] = 2, [TicketStatus.Assigned] = 2, [TicketStatus.InProgress] = 1,
                [TicketStatus.WaitingForEmployee] = 1, [TicketStatus.Resolved] = 2, [TicketStatus.Closed] = 1,
                [TicketStatus.Cancelled] = 2,
            },
            dashboard.ByStatus.ToDictionary(x => x.Status, x => x.Count));
    }

    [SqlServerFact]
    public async Task Trend_has_every_day_of_the_range_by_the_day_in_Israel()
    {
        var s = await SeedScenario();

        var trend = (await Organization(s)).Trend;

        Assert.Equal(12, trend.Count);
        Assert.Equal(Enumerable.Range(0, 12).Select(From.AddDays), trend.Select(p => p.Date));
        Assert.Equal([1, 1, 1, 1, 1, 1, 1, 0, 0, 1, 2, 1], trend.Select(p => p.Opened));
        // T10 on 8/11, T5 on 9/11 (by ClosedAt; the cancelled T8 and T13 have none).
        Assert.Equal([0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0], trend.Select(p => p.Closed));
    }

    [SqlServerFact]
    public async Task Day_boundaries_follow_Israel_time_in_winter_and_in_summer()
    {
        var s = await SeedOrganization();
        await AddTickets(s,
            // Winter (UTC+2): 21:59 UTC = 23:59 on 10/11; 22:00 UTC = 00:00 on 11/11.
            T(s, s.DevId, TicketStatus.New, Utc(11, 10, 21, 59)),
            T(s, s.DevId, TicketStatus.New, Utc(11, 10, 22, 0)),
            // Summer (UTC+3): 20:59 UTC = 23:59 on 14/7; 21:00 UTC = 00:00 on 15/7.
            T(s, s.DevId, TicketStatus.New, new DateTime(2026, 7, 14, 20, 59, 0, DateTimeKind.Utc)),
            T(s, s.DevId, TicketStatus.New, new DateTime(2026, 7, 14, 21, 0, 0, DateTimeKind.Utc)),
            T(s, s.DevId, TicketStatus.New, new DateTime(2026, 7, 15, 1, 0, 0, DateTimeKind.Utc)));

        var winter = await Organization(s, new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 11));
        var summer = await Organization(s, new DateOnly(2026, 7, 14), new DateOnly(2026, 7, 15));

        Assert.Equal([1, 1], winter.Trend.Select(p => p.Opened));
        Assert.Equal([1, 2], summer.Trend.Select(p => p.Opened));
        // The range's UTC bounds follow the same days: 14/7 alone = 13/7 21:00 – 14/7 21:00 UTC.
        Assert.Equal(3, summer.Cards.OpenedInRangeCount);
        var fourteenth = await Organization(s, new DateOnly(2026, 7, 14), new DateOnly(2026, 7, 14));
        Assert.Equal((1, 1), (fourteenth.Cards.OpenedInRangeCount, fourteenth.Trend.Single().Opened));
    }

    [SqlServerFact]
    public async Task Oldest_open_puts_the_overdue_first_then_the_oldest()
    {
        var s = await SeedScenario();

        var oldest = (await Organization(s)).OldestOpen;

        Assert.Equal(["T9", "T2", "T7", "T3", "T11", "T12", "T1"], oldest.Select(t => s.Names[t.Id]));
        Assert.Equal([true, true, false, false, false, false, false], oldest.Select(t => t.IsOverdue));
        var t12 = oldest.Single(t => s.Names[t.Id] == "T12");
        Assert.Equal(("משאבי אנוש", "פיתוח", TicketStatus.Assigned, Utc(11, 14, 8)),
            (t12.RequesterDepartmentName, t12.AssignedDepartmentName, t12.Status, t12.DueAt!.Value));
        var t1 = oldest.Single(t => s.Names[t.Id] == "T1");
        Assert.Equal((true, (string?)null), (t1.IsSubjectExcerpt, t1.AssignedDepartmentName));
        Assert.Equal("תיאור הפנייה T1", t1.Subject);
    }

    [SqlServerFact]
    public async Task Oldest_open_shows_at_most_ten()
    {
        var s = await SeedOrganization();
        await AddTickets(s, Enumerable.Range(0, 12)
            .Select(i => T(s, s.DevId, TicketStatus.New, Now.AddHours(-i - 1))).ToArray());

        var oldest = (await Organization(s)).OldestOpen;

        Assert.Equal(10, oldest.Count);
        Assert.Equal(oldest.OrderBy(t => t.CreatedAt).Select(t => t.Id), oldest.Select(t => t.Id));
    }

    [SqlServerFact]
    public async Task Comparison_is_by_handling_department_with_a_not_routed_row()
    {
        var s = await SeedScenario();

        var rows = (await Organization(s)).Departments;

        Assert.Equal(
            [
                new DashboardDepartmentComparisonDto(s.HrId, "משאבי אנוש", true, 4, 2, 4, 95.5),
                new DashboardDepartmentComparisonDto(s.DevId, "פיתוח", true, 1, 0, 0, null),
                new DashboardDepartmentComparisonDto(null, DashboardMapping.NotRoutedName, true, 2, 0, 0, null),
            ],
            rows);
        Assert.Equal((await Organization(s)).Cards.OpenCount, rows.Sum(r => r.OpenCount));
    }

    [SqlServerFact]
    public async Task Another_organizations_tickets_are_not_counted()
    {
        var s = await SeedScenario();
        var other = await SeedOrganization();
        await AddTickets(other, T(other, other.DevId, TicketStatus.New, Now.AddHours(-1)));

        Assert.Equal(7, (await Organization(s)).Cards.OpenCount);
        Assert.Equal(1, (await Organization(other)).Cards.OpenCount);
    }

    [SqlServerFact]
    public async Task Without_tickets_the_ratios_are_null_and_the_trend_is_zero()
    {
        var s = await SeedOrganization();

        var dashboard = await Organization(s);

        Assert.Equal(new DashboardCardsDto(0, 0, 0, 0, 0, null, null, null), dashboard.Cards);
        Assert.Empty(dashboard.ByCategory);
        Assert.All(dashboard.ByStatus, x => Assert.Equal(0, x.Count));
        Assert.All(dashboard.Trend, p => Assert.Equal((0, 0), (p.Opened, p.Closed)));
        Assert.Empty(dashboard.OldestOpen);
        Assert.Equal(["משאבי אנוש", "פיתוח"], dashboard.Departments.Select(d => d.DepartmentName));
    }

    // ===================== The department dashboard =====================

    [SqlServerFact]
    public async Task Requesters_view_counts_the_tickets_of_the_departments_employees()
    {
        var s = await SeedScenario();

        var dashboard = await DepartmentDashboard(s, s.ManagerId, s.DevId, DashboardView.Requesters);

        Assert.Equal(new DashboardCardsDto(5, 4, 2, 9, 4, 95.5, 75.0, 33.3), dashboard.Cards);
        Assert.Equal(0, dashboard.Trend.Single(p => p.Date == new DateOnly(2026, 11, 11)).Opened);
        Assert.DoesNotContain(dashboard.OldestOpen, t => s.Names[t.Id] is "T11" or "T12");
    }

    [SqlServerFact]
    public async Task Handling_view_counts_the_tickets_the_department_handles()
    {
        var s = await SeedScenario();

        var dev = await DepartmentDashboard(s, s.ManagerId, s.DevId, DashboardView.Handling);
        var hr = await DepartmentDashboard(s, s.ExecutiveId, s.HrId, DashboardView.Handling);

        // Development handles T12 only: nothing resolved – no average and no compliance (not 0).
        Assert.Equal(new DashboardCardsDto(1, 1, 0, 1, 0, null, null, 100.0), dev.Cards);
        Assert.Equal(["T12"], dev.OldestOpen.Select(t => s.Names[t.Id]));
        Assert.Equal((4, 2, 7, 4), (hr.Cards.OpenCount, hr.Cards.OverdueOpenCount, hr.Cards.OpenedInRangeCount,
            hr.Cards.ResolvedInRangeCount));
    }

    // ===================== Cache invalidation through the interceptor =====================

    [SqlServerFact]
    public async Task A_status_change_saved_through_the_interceptor_shows_on_the_next_request()
    {
        var s = await SeedScenario();
        var versions = new TicketDataVersions();
        var cache = new MemoryDashboardCache(new MemoryCache(new MemoryCacheOptions()));

        var before = await Organization(s, cache: cache, versions: versions);
        await using (var db = database.CreateContext(s.ExecutiveId, s.OrgId, new TicketChangesInterceptor(versions)))
        {
            var t1 = await db.Tickets.SingleAsync(t => t.Id == s.Ids["T1"]);
            t1.Status = TicketStatus.Cancelled;
            await db.SaveChangesAsync();
        }

        var after = await Organization(s, cache: cache, versions: versions);

        Assert.Equal(7, before.Cards.OpenCount);
        Assert.Equal(6, after.Cards.OpenCount);
    }

    [SqlServerFact]
    public async Task Inside_a_transaction_the_version_is_bumped_only_after_the_commit()
    {
        var s = await SeedScenario();
        var versions = new TicketDataVersions();
        await using var db = database.CreateContext(s.ExecutiveId, s.OrgId, new TicketChangesInterceptor(versions));
        var unitOfWork = new UnitOfWork(db);

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var t2 = await db.Tickets.SingleAsync(t => t.Id == s.Ids["T2"], ct);
            t2.Status = TicketStatus.InProgress;
            await unitOfWork.SaveChangesAsync(ct);
            Assert.Equal(0, versions.Get(s.OrgId)); // saved, not committed yet
            return 0;
        }, default);

        Assert.Equal(1, versions.Get(s.OrgId));
    }

    [SqlServerFact]
    public async Task A_rolled_back_transaction_does_not_bump()
    {
        var s = await SeedScenario();
        var versions = new TicketDataVersions();
        await using var db = database.CreateContext(s.ExecutiveId, s.OrgId, new TicketChangesInterceptor(versions));

        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var t2 = await db.Tickets.SingleAsync(t => t.Id == s.Ids["T2"]);
            t2.Status = TicketStatus.InProgress;
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        Assert.Equal(0, versions.Get(s.OrgId));
    }

    [SqlServerFact]
    public async Task A_failed_save_does_not_bump()
    {
        var s = await SeedScenario();
        var versions = new TicketDataVersions();
        await using var db = database.CreateContext(s.ExecutiveId, s.OrgId, new TicketChangesInterceptor(versions));
        var t2 = await db.Tickets.SingleAsync(t => t.Id == s.Ids["T2"]);
        db.Entry(t2).Property(t => t.RowVersion).OriginalValue = new byte[8]; // another user changed it meanwhile
        t2.Status = TicketStatus.InProgress;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());

        Assert.Equal(0, versions.Get(s.OrgId));
    }

    [SqlServerFact]
    public async Task Handler_deactivation_bulk_update_bumps_after_its_commit()
    {
        // ExecuteUpdate bypasses SaveChanges: TicketRepository notes the change, the caller's save + commit bumps.
        var s = await SeedOrganization();
        await AddTickets(s, T(s, s.DevId, TicketStatus.InProgress, Now.AddHours(-5), s.PayrollId,
            ClassificationSource.Ai, Urgency.Medium, s.HrId, Now.AddDays(2), configure: t => t.AssignedToUserId = s.HrUserId));
        var versions = new TicketDataVersions();
        await using var db = database.CreateContext(s.ExecutiveId, s.OrgId, new TicketChangesInterceptor(versions));
        var unitOfWork = new UnitOfWork(db);

        var count = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var updated = await new TicketRepository(db).UnassignUnresolvedTicketsAsync(s.HrUserId,
                TicketStatus.InProgress, TicketStatus.Assigned, Now, ct);
            await unitOfWork.SaveChangesAsync(ct); // the history rows (no Ticket entity is tracked)
            return updated;
        }, default);

        Assert.Equal(1, count);
        Assert.Equal(1, versions.Get(s.OrgId));
        Assert.Equal(1, (await Organization(s)).ByStatus.Single(x => x.Status == TicketStatus.Assigned).Count);
    }

    // ===================== Seeding =====================

    private sealed record Seeded(int OrgId, int DevId, int HrId, int PayrollId, int GeneralId, int DevUserId,
        int HrUserId, int ManagerId, int ExecutiveId)
    {
        public Dictionary<string, int> Ids { get; } = [];
        public Dictionary<int, string> Names { get; } = [];
    }

    /// <summary>
    /// An organization: Development (an employee; its manager) and HR (a handler; the executive), categories PAYROLL and
    /// GENERAL (the fallback), both handled by HR.
    /// </summary>
    private async Task<Seeded> SeedOrganization()
    {
        await using var db = database.CreateContext(signedInUserId: null, organizationId: 0);
        var organization = new Organization { Name = "Dashboard tests", IsActive = true, CreatedAt = Now.AddYears(-1) };
        db.Organizations.Add(organization);
        await db.SaveChangesAsync();

        var dev = new Department { OrganizationId = organization.Id, Name = "פיתוח", IsActive = true };
        var hr = new Department { OrganizationId = organization.Id, Name = "משאבי אנוש", IsActive = true };
        db.Departments.AddRange(dev, hr);
        await db.SaveChangesAsync();

        var devUser = NewUser(organization.Id, dev.Id, Role.Employee);
        var hrUser = NewUser(organization.Id, hr.Id, Role.Handler);
        var manager = NewUser(organization.Id, dev.Id, Role.DepartmentManager);
        var executive = NewUser(organization.Id, hr.Id, Role.Executive);
        db.Users.AddRange(devUser, hrUser, manager, executive);
        var payroll = NewCategory(organization.Id, "PAYROLL", Payroll, hr.Id, isFallback: false);
        var general = NewCategory(organization.Id, "GENERAL", General, hr.Id, isFallback: true);
        db.Categories.AddRange(payroll, general);
        await db.SaveChangesAsync();

        dev.ManagerUserId = manager.Id;
        await db.SaveChangesAsync();

        return new Seeded(organization.Id, dev.Id, hr.Id, payroll.Id, general.Id, devUser.Id, hrUser.Id, manager.Id,
            executive.Id);
    }

    /// <summary>The known set of the class summary.</summary>
    private async Task<Seeded> SeedScenario()
    {
        var s = await SeedOrganization();
        var (dev, hr) = (s.DevId, s.HrId);
        var tickets = new (string Name, Ticket Ticket)[]
        {
            ("T1", T(s, dev, TicketStatus.New, Utc(11, 12, 8), subject: null)),
            ("T2", T(s, dev, TicketStatus.Assigned, Utc(11, 10, 10), s.PayrollId, ClassificationSource.Ai, Urgency.High,
                hr, dueAt: Now.AddHours(-1))),
            ("T3", T(s, dev, TicketStatus.WaitingForEmployee, Utc(11, 5, 10), s.GeneralId, ClassificationSource.Manual,
                Urgency.Medium, hr, dueAt: Now.AddDays(-2), configure: t => t.SlaPausedAt = Now.AddDays(-3))),
            // High = 4 hours; paused 24 hours → due 4/11 12:00. Resolved 4/11 08:00: in time only thanks to the pause.
            ("T4", T(s, dev, TicketStatus.Resolved, Utc(11, 3, 8), s.PayrollId, ClassificationSource.Ai, Urgency.High, hr,
                dueAt: Utc(11, 4, 12), resolvedAt: Utc(11, 4, 8), configure: t => t.SlaPausedSeconds = 24 * 3600)),
            // The fallback: no urgency, Medium hours (72).
            ("T5", T(s, dev, TicketStatus.Closed, Utc(11, 2, 0), s.GeneralId, ClassificationSource.Fallback, null, hr,
                dueAt: Utc(11, 5, 0), resolvedAt: Utc(11, 2, 12), closedAt: Utc(11, 9, 13))),
            // Reopened 7/11 06:00 (new clock), resolved again 8/11 06:00: 48 hours from the opening.
            ("T6", T(s, dev, TicketStatus.Resolved, Utc(11, 6, 6), s.PayrollId, ClassificationSource.Manual, Urgency.Medium,
                hr, dueAt: Utc(11, 9, 6), resolvedAt: Utc(11, 8, 6), configure: t => t.SlaRestartedAt = Utc(11, 7, 6))),
            // Reopened on 11/11: its resolution was cleared.
            ("T7", T(s, dev, TicketStatus.InProgress, Utc(11, 4, 10), s.GeneralId, ClassificationSource.Ai, Urgency.Medium,
                hr, dueAt: Utc(11, 14, 0), configure: t => t.SlaRestartedAt = Utc(11, 11, 0))),
            ("T8", T(s, dev, TicketStatus.Cancelled, Utc(11, 7, 10), s.GeneralId, ClassificationSource.Fallback, null, hr,
                dueAt: Utc(11, 10, 10))),
            ("T9", T(s, dev, TicketStatus.InProgress, Utc(10, 1, 10), s.PayrollId, ClassificationSource.Manual,
                Urgency.Medium, hr, dueAt: Utc(10, 5, 10))),
            ("T10", T(s, dev, TicketStatus.Closed, Utc(10, 20, 0), s.PayrollId, ClassificationSource.Manual, Urgency.Medium,
                hr, dueAt: Utc(10, 23, 0), resolvedAt: Utc(11, 1, 10), closedAt: Utc(11, 8, 10))),
            ("T11", T(s, hr, TicketStatus.New, Utc(11, 10, 23, 30))),
            ("T12", T(s, hr, TicketStatus.Assigned, Utc(11, 11, 8), s.PayrollId, ClassificationSource.Ai, Urgency.Medium,
                dev, dueAt: Utc(11, 14, 8))),
            ("T13", T(s, dev, TicketStatus.Cancelled, Utc(10, 31, 22, 30))),
        };

        foreach (var (name, ticket) in tickets)
        {
            ticket.Description = $"תיאור הפנייה {name}";
        }

        await AddTickets(s, tickets.Select(t => t.Ticket).ToArray());
        foreach (var (name, ticket) in tickets)
        {
            s.Ids[name] = ticket.Id;
            s.Names[ticket.Id] = name;
        }

        return s;
    }

    private async Task AddTickets(Seeded s, params Ticket[] tickets)
    {
        await using var db = database.CreateContext(signedInUserId: null, organizationId: 0);
        db.Tickets.AddRange(tickets);
        await db.SaveChangesAsync();
    }

    private static Ticket T(Seeded s, int requesterDepartmentId, TicketStatus status, DateTime createdAt,
        int? categoryId = null, ClassificationSource? source = null, Urgency? urgency = null,
        int? assignedDepartmentId = null, DateTime? dueAt = null, DateTime? resolvedAt = null, DateTime? closedAt = null,
        string? subject = "נושא", Action<Ticket>? configure = null)
    {
        var ticket = new Ticket
        {
            OrganizationId = s.OrgId,
            RequesterId = requesterDepartmentId == s.DevId ? s.DevUserId : s.HrUserId,
            RequesterDepartmentId = requesterDepartmentId,
            Subject = subject,
            Description = "תיאור של הפנייה",
            CategoryId = categoryId,
            ClassificationSource = source,
            Urgency = urgency,
            AssignedDepartmentId = assignedDepartmentId,
            Status = status,
            AiStatus = AiStatus.Completed,
            DueAt = dueAt,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            ResolvedAt = resolvedAt,
            ClosedAt = closedAt,
        };
        configure?.Invoke(ticket);
        return ticket;
    }

    private static User NewUser(int organizationId, int departmentId, Role role)
    {
        var code = Guid.NewGuid().ToString("N")[..12];
        return new User
        {
            OrganizationId = organizationId,
            DepartmentId = departmentId,
            EmployeeCode = $"T{code}",
            FirstName = "בדיקה",
            LastName = role.ToString(),
            Email = $"{code}@dashboard.test",
            JobTitle = "J",
            PasswordHash = "h",
            IsActive = true,
            CreatedAt = Now.AddYears(-1),
            Roles = (role == Role.Employee ? [Role.Employee] : new[] { Role.Employee, role })
                .Select(r => new UserRole { Role = r }).ToList(),
        };
    }

    private static Category NewCategory(int organizationId, string code, string name, int departmentId, bool isFallback) =>
        new()
        {
            OrganizationId = organizationId, Code = code, Name = name, Description = name, IsActive = true,
            IsFallback = isFallback, RoutingType = RoutingType.FixedDepartment, HandlingDepartmentId = departmentId,
        };

    // ===================== The service over SQL Server =====================

    private Task<OrganizationDashboardDto> Organization(Seeded s, DateOnly? from = null, DateOnly? to = null,
        MemoryDashboardCache? cache = null, TicketDataVersions? versions = null) =>
        ServiceFor(s, s.ExecutiveId, cache, versions)
            .GetOrganizationAsync(new OrganizationDashboardQuery { From = from ?? From, To = to ?? To }, default);

    private Task<DepartmentDashboardDto> DepartmentDashboard(Seeded s, int userId, int departmentId, DashboardView view) =>
        ServiceFor(s, userId).GetDepartmentAsync(departmentId,
            new DepartmentDashboardQuery { From = From, To = To, View = view }, default);

    /// <summary>A fresh context and – unless given – a fresh cache, so every call computes.</summary>
    private DashboardService ServiceFor(Seeded s, int userId, MemoryDashboardCache? cache = null,
        TicketDataVersions? versions = null)
    {
        var db = database.CreateContext(userId, s.OrgId);
        var currentUser = new FakeCurrentUser { SignedInUserId = userId, SignedInOrganizationId = s.OrgId };
        return new DashboardService(new DashboardRepository(db), new UserRepository(db), new DepartmentRepository(db),
            new CategoryRepository(db), new MemoryReferenceDataCache(new MemoryCache(new MemoryCacheOptions())),
            cache ?? new MemoryDashboardCache(new MemoryCache(new MemoryCacheOptions())), versions ?? new TicketDataVersions(),
            currentUser, new FakeDateTimeProvider(Now), new DepartmentDashboardQueryValidator(),
            new OrganizationDashboardQueryValidator(), new CapturingLogger<DashboardService>());
    }
}
