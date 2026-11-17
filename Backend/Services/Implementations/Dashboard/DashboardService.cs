using System.Diagnostics;
using System.Globalization;
using Common.DTOs.Dashboard;
using Common.DTOs.Tickets;
using Common.Enums;
using Common.Exceptions;
using Common.Interfaces;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Repositories.Interfaces;
using Services.Interfaces;
using Services.Validators;

namespace Services.Implementations.Dashboard;

/// <summary>
/// The dashboards (task 5.1). Order: 400 (range) ← 403 (role) ← 404 (department outside the scope) ← cache ← the
/// grouped queries of <see cref="IDashboardRepository"/> ← <see cref="DashboardMapping"/>.
/// The viewer is read from the database (roles, managed department), not from the token – like TicketAccess.
/// </summary>
public sealed class DashboardService(
    IDashboardRepository dashboards,
    IUserRepository users,
    IDepartmentRepository departments,
    ICategoryRepository categories,
    IReferenceDataCache referenceData,
    IDashboardCache cache,
    ITicketDataVersion versions,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IValidator<DepartmentDashboardQuery> departmentValidator,
    IValidator<OrganizationDashboardQuery> organizationValidator,
    ILogger<DashboardService> logger) : IDashboardService
{
    public const string DepartmentNotFoundTitle = "המחלקה לא נמצאה";
    public const string NotAllowedTitle = "אין לך הרשאה לצפות בדשבורד זה";

    public async Task<DepartmentDashboardDto> GetDepartmentAsync(int departmentId, DepartmentDashboardQuery query,
        CancellationToken cancellationToken)
    {
        await departmentValidator.EnsureValidAsync(query, cancellationToken);
        var viewer = await GetViewerAsync(cancellationToken);
        var isExecutive = viewer.Roles.Contains(Role.Executive);
        if (!isExecutive && !viewer.Roles.Contains(Role.DepartmentManager))
        {
            throw new ForbiddenException(NotAllowedTitle, errorType: "dashboard-not-allowed");
        }

        // Spec 2.4: a department manager sees their own department only; any other one does not exist for them.
        if (!isExecutive && viewer.ManagedDepartmentId != departmentId)
        {
            throw new NotFoundException(DepartmentNotFoundTitle);
        }

        var organizationId = currentUser.OrganizationId;
        var department = (await ReferenceDataLookups.DepartmentsAsync(referenceData, departments, organizationId,
                             cancellationToken))
                         .FirstOrDefault(d => d.Id == departmentId)
                         ?? throw new NotFoundException(DepartmentNotFoundTitle);

        var view = query.View ?? DashboardView.Requesters;
        var (range, today, now) = Range(query);
        var key = Key(organizationId, department.Id.ToString(CultureInfo.InvariantCulture), view.ToString(), range);

        return await CachedAsync(key, $"department {departmentId} {view}", range, async ct =>
        {
            var scope = new DashboardScope(department.Id, view);
            var window = DashboardRange.Window(range, today, now);
            var metrics = await MetricsAsync(scope, range, window, organizationId, ct);
            return new DepartmentDashboardDto(
                new DashboardDepartmentDto(department.Id, department.Name, department.IsActive),
                view, range, now, metrics.Cards, metrics.ByCategory, metrics.ByStatus, metrics.Trend, metrics.OldestOpen);
        }, cancellationToken);
    }

    public async Task<OrganizationDashboardDto> GetOrganizationAsync(OrganizationDashboardQuery query,
        CancellationToken cancellationToken)
    {
        await organizationValidator.EnsureValidAsync(query, cancellationToken);
        var viewer = await GetViewerAsync(cancellationToken);
        if (!viewer.Roles.Contains(Role.Executive))
        {
            throw new ForbiddenException(NotAllowedTitle, errorType: "dashboard-not-allowed");
        }

        var organizationId = currentUser.OrganizationId;
        var (range, today, now) = Range(query);
        var key = Key(organizationId, "org", "-", range);

        return await CachedAsync(key, "organization", range, async ct =>
        {
            var window = DashboardRange.Window(range, today, now);
            var metrics = await MetricsAsync(DashboardScope.Organization, range, window, organizationId, ct);
            var stats = await dashboards.GetHandlingDepartmentStatsAsync(window, ct);
            var allDepartments = await ReferenceDataLookups.DepartmentsAsync(referenceData, departments, organizationId, ct);
            return new OrganizationDashboardDto(range, now, metrics.Cards, metrics.ByCategory, metrics.ByStatus,
                metrics.Trend, metrics.OldestOpen, DashboardMapping.Comparison(stats, allDepartments));
        }, cancellationToken);
    }

    /// <summary>
    /// Spec 11.3: dashboard:{orgId}:{version}:{deptId}:{view}:{from}:{to}. The version is read before the queries run:
    /// a change during them bumps it, so the result is stored under a key nobody asks for any more.
    /// </summary>
    internal string Key(int organizationId, string departmentPart, string viewPart, DashboardRangeDto range) =>
        string.Create(CultureInfo.InvariantCulture,
            $"dashboard:{organizationId}:{versions.Get(organizationId)}:{departmentPart}:{viewPart}:{range.From:yyyy-MM-dd}:{range.To:yyyy-MM-dd}");

    private (DashboardRangeDto Range, DateOnly Today, DateTime Now) Range(DashboardRangeQuery query)
    {
        var now = clock.UtcNow;
        var today = DashboardRange.Today(now);
        return (DashboardRange.Resolve(query, today), today, now);
    }

    private async Task<T> CachedAsync<T>(string key, string scopeName, DashboardRangeDto range,
        Func<CancellationToken, Task<T>> compute, CancellationToken cancellationToken) where T : class
    {
        var started = Stopwatch.GetTimestamp();
        var (value, hit) = await cache.GetOrCreateAsync(key, compute, cancellationToken);
        logger.LogInformation(
            "Dashboard {Scope} {From:yyyy-MM-dd}..{To:yyyy-MM-dd} for user {UserId}: {CacheResult} in {ElapsedMs:0} ms",
            scopeName, range.From, range.To, currentUser.UserId, hit ? "cache hit" : "computed",
            Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return value;
    }

    /// <summary>The six queries every dashboard shares, one after the other on the request's context.</summary>
    private async Task<SharedMetrics> MetricsAsync(DashboardScope scope, DashboardRangeDto range, DashboardWindow window,
        int organizationId, CancellationToken cancellationToken)
    {
        var snapshot = await dashboards.GetSnapshotAsync(scope, window, cancellationToken);
        var opened = await dashboards.GetOpenedGroupsAsync(scope, window, cancellationToken);
        var resolved = await dashboards.GetResolvedSummaryAsync(scope, window, cancellationToken);
        var openedPerDay = await dashboards.GetOpenedPerDayAsync(scope, window, cancellationToken);
        var closedPerDay = await dashboards.GetClosedPerDayAsync(scope, window, cancellationToken);
        var oldest = await dashboards.GetOldestOpenAsync(scope, window, DashboardLimits.OldestOpenCount, cancellationToken);
        var allCategories = await ReferenceDataLookups.CategoriesAsync(referenceData, categories, organizationId,
            cancellationToken);

        return new SharedMetrics(
            DashboardMapping.Cards(snapshot, opened, resolved),
            DashboardMapping.ByCategory(opened, allCategories),
            DashboardMapping.ByStatus(opened),
            DashboardMapping.Trend(range, openedPerDay, closedPerDay),
            oldest);
    }

    private async Task<TicketViewer> GetViewerAsync(CancellationToken cancellationToken) =>
        await users.GetTicketViewerAsync(currentUser.UserId, cancellationToken)
        ?? throw new AuthenticationFailedException(AuthenticationFailedException.NotSignedInTitle,
            errorType: "unauthorized");

    private sealed record SharedMetrics(
        DashboardCardsDto Cards,
        IReadOnlyList<DashboardCategoryCountDto> ByCategory,
        IReadOnlyList<DashboardStatusCountDto> ByStatus,
        IReadOnlyList<DashboardTrendPointDto> Trend,
        IReadOnlyList<DashboardTicketDto> OldestOpen);
}
