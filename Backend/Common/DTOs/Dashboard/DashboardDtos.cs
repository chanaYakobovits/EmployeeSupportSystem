using Common.Enums;

namespace Common.DTOs.Dashboard;

// The dashboards (spec 7.9, 9.3). Every number is computed by the database; the definitions of the metrics are in one
// place – DashboardMetrics in the Repositories layer.

/// <summary>The resolved range: the days the range metrics, the charts and the trend cover (Israel time, both included).</summary>
public sealed record DashboardRangeDto(DateOnly From, DateOnly To);

/// <summary>The metric cards (spec 7.9).</summary>
/// <param name="OpenCount">Now (not the range): not resolved, closed or cancelled – New, Assigned, InProgress, WaitingForEmployee.</param>
/// <param name="NewThisWeekCount">Opened in the last 7 days, today included (Israel time) – not the range.</param>
/// <param name="OverdueOpenCount">Now: past the due time while the SLA clock runs (WaitingForEmployee is paused).</param>
/// <param name="OpenedInRangeCount">Opened in the range – the base of the charts and of <paramref name="AutoClassifiedPercent"/>.</param>
/// <param name="ResolvedInRangeCount">Resolved in the range (the last resolution of a reopened ticket).</param>
/// <param name="AverageResolutionHours">Of the tickets resolved in the range: opening → last resolution; null without any.</param>
/// <param name="SlaCompliancePercent">Of the tickets resolved in the range: resolved by the due time; null without any.</param>
/// <param name="AutoClassifiedPercent">Of the tickets opened in the range: classified by the AI; null without any.</param>
public sealed record DashboardCardsDto(
    int OpenCount,
    int NewThisWeekCount,
    int OverdueOpenCount,
    int OpenedInRangeCount,
    int ResolvedInRangeCount,
    double? AverageResolutionHours,
    double? SlaCompliancePercent,
    double? AutoClassifiedPercent);

/// <summary>Opened in the range, by category. CategoryId null = not classified yet (CategoryName null too).</summary>
public sealed record DashboardCategoryCountDto(int? CategoryId, string? CategoryName, int Count);

/// <summary>Opened in the range, by their current status. Every status, in the enum's order, zeros included.</summary>
public sealed record DashboardStatusCountDto(TicketStatus Status, int Count);

/// <summary>One day of the trend (Israel time): opened that day vs. closed that day. Every day of the range.</summary>
public sealed record DashboardTrendPointDto(DateOnly Date, int Opened, int Closed);

/// <summary>A row of "the oldest / overdue open tickets": overdue first, then the oldest.</summary>
/// <param name="Subject">The subject, or – when the ticket has none – the start of the description.</param>
public sealed record DashboardTicketDto(
    int Id,
    string Subject,
    bool IsSubjectExcerpt,
    string RequesterDepartmentName,
    string? AssignedDepartmentName,
    TicketStatus Status,
    DateTime CreatedAt,
    DateTime? DueAt,
    bool IsOverdue);

public sealed record DashboardDepartmentDto(int Id, string Name, bool IsActive);

/// <summary>GET /api/dashboard/departments/{id}.</summary>
/// <param name="GeneratedAt">When the numbers were computed – up to a minute ago (cache, spec 11.3).</param>
public sealed record DepartmentDashboardDto(
    DashboardDepartmentDto Department,
    DashboardView View,
    DashboardRangeDto Range,
    DateTime GeneratedAt,
    DashboardCardsDto Cards,
    IReadOnlyList<DashboardCategoryCountDto> ByCategory,
    IReadOnlyList<DashboardStatusCountDto> ByStatus,
    IReadOnlyList<DashboardTrendPointDto> Trend,
    IReadOnlyList<DashboardTicketDto> OldestOpen);

/// <summary>
/// A row of the executive's comparison between departments – by the handling department (AssignedDepartmentId),
/// whose performance the three numbers measure. DepartmentId null = tickets not routed to any department yet.
/// </summary>
/// <param name="OpenCount">Now, as <see cref="DashboardCardsDto.OpenCount"/>.</param>
/// <param name="OverdueOpenCount">Now, as <see cref="DashboardCardsDto.OverdueOpenCount"/>.</param>
/// <param name="ResolvedInRangeCount">The base of <paramref name="AverageResolutionHours"/>.</param>
public sealed record DashboardDepartmentComparisonDto(
    int? DepartmentId,
    string? DepartmentName,
    bool IsActive,
    int OpenCount,
    int OverdueOpenCount,
    int ResolvedInRangeCount,
    double? AverageResolutionHours);

/// <summary>GET /api/dashboard/organization: the same metrics for the whole organization + the comparison.</summary>
public sealed record OrganizationDashboardDto(
    DashboardRangeDto Range,
    DateTime GeneratedAt,
    DashboardCardsDto Cards,
    IReadOnlyList<DashboardCategoryCountDto> ByCategory,
    IReadOnlyList<DashboardStatusCountDto> ByStatus,
    IReadOnlyList<DashboardTrendPointDto> Trend,
    IReadOnlyList<DashboardTicketDto> OldestOpen,
    IReadOnlyList<DashboardDepartmentComparisonDto> Departments);
