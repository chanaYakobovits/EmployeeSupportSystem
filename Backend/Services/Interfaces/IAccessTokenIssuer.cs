using Common.DTOs.Auth;

namespace Services.Interfaces;

/// <summary>
/// Creates a signed access token (JWT, spec 6.1). Implemented in the API layer, next to the token validation,
/// so the signing key and the token library stay in one place.
/// </summary>
public interface IAccessTokenIssuer
{
    AccessToken Issue(AccessTokenSubject subject);
}
