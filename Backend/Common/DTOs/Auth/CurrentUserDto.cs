namespace Common.DTOs.Auth;

/// <summary>The signed-in user (GET /api/auth/me, spec 9.1).</summary>
public sealed record CurrentUserDto(
    int Id,
    string EmployeeCode,
    string FirstName,
    string LastName,
    string Email,
    string JobTitle,
    DepartmentSummaryDto Department,
    IReadOnlyList<string> Roles,
    bool MustChangePassword);

public sealed record DepartmentSummaryDto(int Id, string Name);
