namespace Common.DTOs.Auth;

/// <summary>
/// POST /api/auth/set-password (anonymous): the token from the invitation link + the new password.
/// Required fields and lengths: SetPasswordRequestValidator; the password policy (spec 6.1) is checked by the service.
/// </summary>
public sealed record SetPasswordRequest(string Token, string NewPassword);
