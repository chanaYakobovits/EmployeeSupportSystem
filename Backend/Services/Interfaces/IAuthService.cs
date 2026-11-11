using Common.DTOs.Auth;

namespace Services.Interfaces;

/// <summary>
/// Authentication (spec 4.1, 6.1). Knows nothing about HTTP: refresh token values come in and go out as strings,
/// and the controller moves them to and from the cookie.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Throws <see cref="Common.Exceptions.AuthenticationFailedException"/> with one uniform message
    /// for every failure, or <see cref="Common.Exceptions.AccountLockedException"/> while locked out.
    /// </summary>
    /// <param name="ipAddress">Client address, recorded in the audit log only.</param>
    Task<AuthResult> LoginAsync(LoginRequest request, string ipAddress, CancellationToken cancellationToken);

    /// <summary>
    /// Rotates the refresh token: the presented one is revoked and replaced. Throws
    /// <see cref="Common.Exceptions.AuthenticationFailedException"/> (401) if it is missing, unknown, expired or revoked;
    /// presenting a revoked token (reuse) also revokes all of the user's tokens.
    /// </summary>
    Task<AuthResult> RefreshAsync(string? refreshToken, string ipAddress, CancellationToken cancellationToken);

    /// <summary>Revokes the token if it is still active. Never throws for a missing or invalid token.</summary>
    Task LogoutAsync(string? refreshToken, string ipAddress, CancellationToken cancellationToken);

    /// <summary>
    /// Spec 7.2, for the signed-in user. Throws <see cref="Common.Exceptions.ValidationException"/> (400) for a wrong
    /// current password or a new password that breaks the policy. On success all refresh tokens are revoked
    /// and a new session is issued.
    /// </summary>
    Task<AuthResult> ChangePasswordAsync(ChangePasswordRequest request, string ipAddress, CancellationToken cancellationToken);

    /// <summary>The signed-in user (from <see cref="Common.Interfaces.ICurrentUser"/>).</summary>
    Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken cancellationToken);
}
