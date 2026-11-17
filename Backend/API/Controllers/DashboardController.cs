using API.Auth;
using Common.DTOs.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

/// <summary>
/// Dashboards (spec 9.3, 7.9). Thin: scope, range and metrics are in <see cref="IDashboardService"/>, which also checks
/// the roles against the database. Range: from/to – days in Israel, both included (default: the last 30 days, at most
/// 366). Cached for 60 seconds, invalidated by any ticket change (spec 11.3).
/// </summary>
[ApiController]
[Route("api/dashboard")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
public class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    /// <summary>
    /// A department's dashboard: view=requesters (default – its employees' tickets) or handling (the tickets it
    /// handles). Department manager – their own department only (else 404); executive – any department.
    /// </summary>
    [HttpGet("departments/{id:int}")]
    [Authorize(Policy = Policies.DashboardViewer)]
    [ProducesResponseType<DepartmentDashboardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DepartmentDashboardDto>> GetDepartment(int id,
        [FromQuery] DepartmentDashboardQuery query, CancellationToken cancellationToken) =>
        Ok(await dashboardService.GetDepartmentAsync(id, query, cancellationToken));

    /// <summary>The organization's dashboard and the comparison by handling department. Executive only.</summary>
    [HttpGet("organization")]
    [Authorize(Policy = Policies.Executive)]
    [ProducesResponseType<OrganizationDashboardDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OrganizationDashboardDto>> GetOrganization(
        [FromQuery] OrganizationDashboardQuery query, CancellationToken cancellationToken) =>
        Ok(await dashboardService.GetOrganizationAsync(query, cancellationToken));
}
