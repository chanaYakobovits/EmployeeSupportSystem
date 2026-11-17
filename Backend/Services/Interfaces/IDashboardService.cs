using Common.DTOs.Dashboard;

namespace Services.Interfaces;

/// <summary>The dashboards (task 5.1, spec 4.5, 7.9, 9.3). Computed by the database, cached for 60 seconds (spec 11.3).</summary>
public interface IDashboardService
{
    /// <summary>
    /// A department's dashboard: its employees' tickets (view=requesters, the default) or the tickets it handles
    /// (view=handling). The executive – any department of the organization; a department manager – only the department
    /// they manage (another one, or one that does not exist → 404). Anyone else → 403.
    /// </summary>
    Task<DepartmentDashboardDto> GetDepartmentAsync(int departmentId, DepartmentDashboardQuery query,
        CancellationToken cancellationToken);

    /// <summary>The organization's dashboard with the comparison between departments. The executive only (else 403).</summary>
    Task<OrganizationDashboardDto> GetOrganizationAsync(OrganizationDashboardQuery query,
        CancellationToken cancellationToken);
}
