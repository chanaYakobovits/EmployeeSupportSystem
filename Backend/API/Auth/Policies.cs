using Common.Enums;

namespace API.Auth;

/// <summary>
/// Authorization policy names (spec 6.2). Every policy except <see cref="PasswordChangePending"/>
/// also requires that the user has no pending password change.
/// </summary>
public static class Policies
{
    public const string Employee = nameof(Role.Employee);
    public const string Handler = nameof(Role.Handler);
    public const string DepartmentManager = nameof(Role.DepartmentManager);
    public const string Executive = nameof(Role.Executive);
    public const string Admin = nameof(Role.Admin);

    /// <summary>
    /// Signed in, even with a pending password change. Only for GET /auth/me and (part B) POST /auth/change-password.
    /// </summary>
    public const string PasswordChangePending = nameof(PasswordChangePending);
}
