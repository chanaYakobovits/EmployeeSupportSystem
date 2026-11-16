using Common.DTOs.Users;

namespace Common.DTOs.Departments;

/// <summary>GET /api/departments.</summary>
public sealed record DepartmentDto(
    int Id,
    string Name,
    bool IsActive,
    UserSummaryDto? Manager,
    // The departmental HR handler (task 3.3), or null: the department's HR tickets go to central HR.
    UserSummaryDto? HrUser,
    int ActiveUserCount,
    // Active users of the department with the Handler role.
    int HandlerCount);

/// <summary>POST /api/departments. A new department is always active.</summary>
public sealed record CreateDepartmentRequest(string Name, int? ManagerUserId, int? HrUserId = null);

/// <summary>
/// PUT /api/departments/{id}. Null <see cref="ManagerUserId"/> = no manager; null <see cref="HrUserId"/> = no
/// departmental HR (its HR tickets go to central HR).
/// </summary>
public sealed record UpdateDepartmentRequest(string Name, int? ManagerUserId, bool? IsActive, int? HrUserId = null);
