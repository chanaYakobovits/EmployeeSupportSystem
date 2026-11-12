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
}

/// <summary>Closed list of sortBy values for GET /api/users (spec 9.4).</summary>
public static class UserSortFields
{
    /// <summary>Last name, then first name. The default.</summary>
    public const string Name = "name";
    public const string EmployeeCode = "employeeCode";
    public const string Email = "email";
    public const string Department = "department";
    public const string CreatedAt = "createdAt";
    public const string LastLoginAt = "lastLoginAt";

    public static readonly IReadOnlyList<string> All = [Name, EmployeeCode, Email, Department, CreatedAt, LastLoginAt];
}
