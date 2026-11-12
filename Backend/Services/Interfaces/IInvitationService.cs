using Common.DTOs.Auth;

namespace Services.Interfaces;

/// <summary>The receiving end of an invitation e-mail (anonymous).</summary>
public interface IInvitationService
{
    /// <summary>
    /// Sets the first password with the token from the link. The link works once: on success it is marked used
    /// atomically, every other invitation of the user is revoked and every session signed out.
    /// </summary>
    /// <exception cref="Common.Exceptions.ValidationException">
    /// Missing fields or a password that breaks the policy (→ 400 with field errors); or a link that is unknown,
    /// used, revoked or expired, or whose user is inactive – one uniform message for all of them (→ 400).
    /// </exception>
    Task SetPasswordAsync(SetPasswordRequest request, string ipAddress, CancellationToken cancellationToken);
}
