namespace Common.DTOs.Auth;

/// <summary>
/// Body of a successful login, refresh or password change. The refresh token is never part of it –
/// it travels only in the HttpOnly cookie.
/// </summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    bool MustChangePassword,
    CurrentUserDto User);
