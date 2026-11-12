using API.Auth;
using API.Extensions;
using Common.DTOs.Paging;
using Common.DTOs.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

/// <summary>
/// User administration (spec 9.3), Admin only. Thin: every rule is in <see cref="IUserService"/>.
/// A user of another organization is not found (404) – the organization query filter hides it.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class UsersController(IUserService userService) : ControllerBase
{
    /// <summary>Paged list with search and filters (spec 9.4).</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<UserListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<UserListItemDto>>> GetPage([FromQuery] UserListQuery query,
        CancellationToken cancellationToken) =>
        Ok(await userService.GetPageAsync(query, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<UserDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDetailsDto>> Get(int id, CancellationToken cancellationToken) =>
        Ok(await userService.GetAsync(id, cancellationToken));

    /// <summary>Creates a user (spec 4.2). The temporary password is in this response only – show it once.</summary>
    [HttpPost]
    [ProducesResponseType<CreateUserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CreateUserResponse>> Create(CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var response = await userService.CreateAsync(request, HttpContext.ClientIp(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = response.User.Id }, response);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<UserDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserDetailsDto>> Update(int id, UpdateUserRequest request,
        CancellationToken cancellationToken) =>
        Ok(await userService.UpdateAsync(id, request, HttpContext.ClientIp(), cancellationToken));

    /// <summary>New temporary password; the user must change it and is signed out everywhere.</summary>
    [HttpPost("{id:int}/reset-password")]
    [ProducesResponseType<ResetPasswordResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ResetPasswordResponse>> ResetPassword(int id, CancellationToken cancellationToken) =>
        Ok(await userService.ResetPasswordAsync(id, HttpContext.ClientIp(), cancellationToken));

    /// <summary>Deactivate / activate. Deactivation signs the user out and frees their unresolved tickets.</summary>
    [HttpPatch("{id:int}/active")]
    [ProducesResponseType<SetUserActiveResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SetUserActiveResponse>> SetActive(int id, SetUserActiveRequest request,
        CancellationToken cancellationToken) =>
        Ok(await userService.SetActiveAsync(id, request, HttpContext.ClientIp(), cancellationToken));
}
