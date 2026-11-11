namespace Common.DTOs.Auth;

/// <summary>
/// What the auth service hands to the HTTP layer: the response body, and the new refresh token
/// that the controller writes to the cookie.
/// </summary>
public sealed record AuthResult(AuthResponse Body, IssuedRefreshToken RefreshToken);

/// <summary>A newly issued refresh token. <see cref="Value"/> is a secret: never log it, never store it unhashed.</summary>
public sealed record IssuedRefreshToken(string Value, DateTime ExpiresAt)
{
    // Keeps the value out of logs and debugger output if the record is ever printed.
    public override string ToString() => $"IssuedRefreshToken {{ ExpiresAt = {ExpiresAt:O} }}";
}
