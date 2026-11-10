namespace Common.DTOs.Auth;

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAt,
    bool MustChangePassword,
    CurrentUserDto User);
