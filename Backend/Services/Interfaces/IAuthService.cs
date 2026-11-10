using Common.DTOs.Auth;

namespace Services.Interfaces;

public interface IAuthService
{
    /// <summary>
    /// Spec 4.1. Throws <see cref="Common.Exceptions.AuthenticationFailedException"/> with one uniform message
    /// for every failure, or <see cref="Common.Exceptions.AccountLockedException"/> while locked out.
    /// </summary>
    /// <param name="ipAddress">Client address, recorded in the audit log only.</param>
    Task<LoginResponse> LoginAsync(LoginRequest request, string ipAddress, CancellationToken cancellationToken);

    /// <summary>The signed-in user (from <see cref="Common.Interfaces.ICurrentUser"/>).</summary>
    Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken cancellationToken);
}
