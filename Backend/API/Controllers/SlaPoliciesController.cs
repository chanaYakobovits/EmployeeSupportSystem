using API.Auth;
using API.Extensions;
using Common.DTOs.Sla;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

/// <summary>SLA targets (spec 3.4, 9.3): read by every signed-in user, changed by Admin only, all four levels together.</summary>
[ApiController]
[Route("api/sla-policies")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public class SlaPoliciesController(ISlaPolicyService slaPolicyService) : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType<SlaPoliciesDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SlaPoliciesDto>> Get(CancellationToken cancellationToken) =>
        Ok(await slaPolicyService.GetAsync(cancellationToken));

    /// <summary>Applies to new tickets and to tickets whose urgency changes; existing due dates are kept.</summary>
    [HttpPut]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<SlaPoliciesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SlaPoliciesDto>> Update(UpdateSlaPoliciesRequest request,
        CancellationToken cancellationToken) =>
        Ok(await slaPolicyService.UpdateAsync(request, HttpContext.ClientIp(), cancellationToken));
}
