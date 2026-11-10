using API.Auth;
using API.Filters;
using Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Dev;

/// <summary>
/// TEMPORARY – development only. Protected endpoints for checking the authorization policies by hand (API.http)
/// before the real protected controllers exist. Delete once TicketsController is in place.
/// </summary>
[ApiController]
[Route("api/dev/auth")]
[DevelopmentOnly]
public class DevAuthController(ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Default policy: signed in, no pending password change.</summary>
    [HttpGet("protected")]
    public IActionResult Protected() => Ok(Describe());

    [HttpGet("admin")]
    [Authorize(Policy = Policies.Admin)]
    public IActionResult AdminOnly() => Ok(Describe());

    private object Describe() => new
    {
        currentUser.UserId,
        currentUser.OrganizationId,
        currentUser.DepartmentId,
        Roles = currentUser.Roles.Select(r => r.ToString()),
        currentUser.MustChangePassword,
    };
}
