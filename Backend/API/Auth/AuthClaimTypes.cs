namespace API.Auth;

/// <summary>
/// Claim names inside the access token. Short JWT names; inbound claim mapping is turned off,
/// so the same names are read back from the validated token.
/// </summary>
public static class AuthClaimTypes
{
    public const string UserId = "sub";
    public const string OrganizationId = "org_id";
    public const string DepartmentId = "dept_id";
    public const string Role = "role";
    public const string MustChangePassword = "must_change_password";
    public const string TokenId = "jti";
}
