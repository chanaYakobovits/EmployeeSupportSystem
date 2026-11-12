namespace Common.DTOs.Auth;

/// <summary>
/// Spec 7.2. Required fields and lengths: ChangePasswordRequestValidator (Services);
/// the password policy itself (spec 6.1) is checked by the service.
/// </summary>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
