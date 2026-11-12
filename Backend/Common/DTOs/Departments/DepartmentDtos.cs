using Common.DTOs.Users;

namespace Common.DTOs.Departments;

/// <summary>GET /api/departments.</summary>
public sealed record DepartmentDto(
    int Id,
    string Name,
    bool IsActive,
    UserSummaryDto? Manager,
    int ActiveUserCount,
    // Active users of the department with the Handler role.
    int HandlerCount);

/// <summary>POST /api/departments. A new department is always active.</summary>
public sealed record CreateDepartmentRequest(string Name, int? ManagerUserId);

/// <summary>PUT /api/departments/{id}. Null <see cref="ManagerUserId"/> = no manager.</summary>
public sealed record UpdateDepartmentRequest(string Name, int? ManagerUserId, bool? IsActive);
