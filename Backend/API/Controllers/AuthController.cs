using API.Auth;
using API.Extensions;
using Common.DTOs.Auth;
using Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Services.Interfaces;

namespace API.Controllers;

/// <summary>
/// Spec 9.1. The only place that reads or writes the refresh-token cookie; AuthService never sees HTTP.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    public const string RefreshCookieName = "refresh_token";

    /// <summary>The cookie is sent only to the auth endpoints, never to the rest of the API.</summary>
    public const string RefreshCookiePath = "/api/auth";

    /// <summary>Login with e-mail or employee code + password (spec 4.1). Sets the refresh-token cookie.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthExtensions.LoginRateLimitPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, ClientIp, cancellationToken);
        return SessionResponse(result);
    }

    /// <summary>
    /// New access token + rotated refresh-token cookie. Anonymous: authenticated by the cookie, so it works after the
    /// access token expired and while a password change is pending.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken cancellationToken)
    {
        try
        {
            var result = await authService.RefreshAsync(Request.Cookies[RefreshCookieName], ClientIp, cancellationToken);
            return SessionResponse(result);
        }
        catch (AuthenticationFailedException)
        {
            // The cookie is useless now; remove it so the client stops sending it.
            DeleteRefreshCookie();
            throw;
        }
    }

    /// <summary>
    /// Revokes the refresh token and deletes the cookie. Always 204 – also with a missing, expired or revoked token
    /// (decision in CLAUDE.md: anonymous, so logging out never fails).
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(Request.Cookies[RefreshCookieName], ClientIp, cancellationToken);
        DeleteRefreshCookie();
        return NoContent();
    }

    /// <summary>
    /// Spec 7.2. Allowed while a password change is pending. Rate limited per user (5 per 15 minutes).
    /// On success all previous refresh tokens are revoked and a new session (token + cookie) is returned.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize(Policy = Policies.PasswordChangePending)]
    [EnableRateLimiting(AuthExtensions.ChangePasswordRateLimitPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> ChangePassword(ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.ChangePasswordAsync(request, ClientIp, cancellationToken);
        return SessionResponse(result);
    }

    /// <summary>The signed-in user, roles and department. Allowed also while a password change is pending.</summary>
    [HttpGet("me")]
    [Authorize(Policy = Policies.PasswordChangePending)]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken cancellationToken) =>
        Ok(await authService.GetCurrentUserAsync(cancellationToken));

    private string ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

    private ActionResult<AuthResponse> SessionResponse(AuthResult result)
    {
        Response.Cookies.Append(RefreshCookieName, result.RefreshToken.Value,
            RefreshCookieOptions(new DateTimeOffset(result.RefreshToken.ExpiresAt, TimeSpan.Zero)));
        return Ok(result.Body);
    }

    private void DeleteRefreshCookie() =>
        // Same Path/Secure/SameSite as when set, or the browser keeps the original cookie.
        Response.Cookies.Delete(RefreshCookieName, RefreshCookieOptions(expires: null));

    /// <summary>Spec 6.1: HttpOnly, Secure, SameSite=Strict.</summary>
    private static CookieOptions RefreshCookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = RefreshCookiePath,
        Expires = expires,
        IsEssential = true,
    };
}
