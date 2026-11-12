using Common.DTOs.Auth;
using Common.Enums;

namespace Common.DTOs.Users;

/// <summary>Row of the users list (GET /api/users).</summary>
public sealed record UserListItemDto(
    int Id,
    string EmployeeCode,
    string FirstName,
    string LastName,
    string Email,
    string JobTitle,
    DepartmentSummaryDto Department,
    IReadOnlyList<Role> Roles,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

/// <summary>One user (GET/PUT /api/users/{id}).</summary>
public sealed record UserDetailsDto(
    int Id,
    string EmployeeCode,
    string FirstName,
    string LastName,
    string Email,
    string JobTitle,
    DepartmentSummaryDto Department,
    IReadOnlyList<Role> Roles,
    bool IsActive,
    bool MustChangePassword,
    bool IsLockedOut,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    // The department this user manages (at most one), or null.
    DepartmentSummaryDto? ManagedDepartment);

/// <summary>Short reference to a user, e.g. a department manager.</summary>
public sealed record UserSummaryDto(int Id, string EmployeeCode, string FirstName, string LastName);

/// <summary>
/// POST /api/users (spec 4.2). The employee code is generated; Employee is always added to <see cref="Roles"/>.
/// DepartmentManager cannot be chosen here – it follows from the departments screen.
/// </summary>
public sealed record CreateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    int DepartmentId,
    string JobTitle,
    IReadOnlyList<Role>? Roles);

/// <summary>PUT /api/users/{id}. The employee code is not part of the request: it never changes.</summary>
public sealed record UpdateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    int DepartmentId,
    string JobTitle,
    IReadOnlyList<Role>? Roles);

/// <summary>
/// The temporary password appears only in this response (spec 4.2): never stored in clear text, logged or audited.
/// </summary>
public sealed record CreateUserResponse(UserDetailsDto User, string TemporaryPassword);

/// <summary>POST /api/users/{id}/reset-password. Shown to the admin once.</summary>
public sealed record ResetPasswordResponse(string TemporaryPassword);

/// <summary>PATCH /api/users/{id}/active. Nullable so that a missing value is a validation error, not "false".</summary>
public sealed record SetUserActiveRequest(bool? IsActive);

/// <summary>
/// <see cref="UnassignedTicketCount"/>: unresolved tickets of a deactivated handler returned to their department queue.
/// </summary>
public sealed record SetUserActiveResponse(UserDetailsDto User, int UnassignedTicketCount);
