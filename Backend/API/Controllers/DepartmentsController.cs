using API.Auth;
using API.Extensions;
using Common.DTOs.Departments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

/// <summary>Departments (spec 9.3): read by every signed-in user, changed by Admin only.</summary>
[ApiController]
[Route("api/departments")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public class DepartmentsController(IDepartmentService departmentService) : ControllerBase
{
    /// <summary>Active departments, by name. <paramref name="includeInactive"/> is honored for Admin only.</summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<DepartmentDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> GetAll([FromQuery] bool includeInactive,
        CancellationToken cancellationToken) =>
        Ok(await departmentService.GetAllAsync(includeInactive, cancellationToken));

    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<DepartmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DepartmentDto>> Create(CreateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        var department = await departmentService.CreateAsync(request, HttpContext.ClientIp(), cancellationToken);
        // No GET by id: the list is the read endpoint.
        return Created("/api/departments", department);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<DepartmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DepartmentDto>> Update(int id, UpdateDepartmentRequest request,
        CancellationToken cancellationToken) =>
        Ok(await departmentService.UpdateAsync(id, request, HttpContext.ClientIp(), cancellationToken));
}
