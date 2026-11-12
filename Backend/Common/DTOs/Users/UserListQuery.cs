using Common.DTOs.Paging;
using Common.Enums;

namespace Common.DTOs.Users;

/// <summary>GET /api/users. All filters are optional and combined with AND.</summary>
public sealed class UserListQuery : PageQuery
{
    /// <summary>Matches first name, last name, full name, e-mail or employee code (contains).</summary>
    public string? Search { get; set; }

    public int? DepartmentId { get; set; }

    public Role? Role { get; set; }

    public bool? IsActive { get; set; }

    /// <summary>State of the latest invitation (as in the list column). Users never invited never match.</summary>
    public InvitationStatus? InvitationStatus { get; set; }
}

/// <summary>Closed list of sortBy values for GET /api/users (spec 9.4).</summary>
public static class UserSortFields
{
    /// <summary>Last name, then first name.</summary>
    public const string Name = "name";
    /// <summary>The default, descending: the newest employees first (the code is a running number).</summary>
    public const string EmployeeCode = "employeeCode";
    public const string Email = "email";
    public const string Department = "department";
    public const string CreatedAt = "createdAt";
    public const string LastLoginAt = "lastLoginAt";

    /// <summary>Used when sortBy is not sent. Same default as the client (users-list.store.ts).</summary>
    public const string Default = EmployeeCode;

    /// <summary>
    /// The direction when sortDir is not sent: the default column sorts descending, any other column ascending.
    /// </summary>
    public static bool IsDescendingByDefault(string sortBy) => sortBy == Default;

    public static readonly IReadOnlyList<string> All = [Name, EmployeeCode, Email, Department, CreatedAt, LastLoginAt];
}
