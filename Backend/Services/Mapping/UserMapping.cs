using Common.DTOs.Users;
using Common.Enums;
using Common.Models;

namespace Services.Mapping;

/// <summary>Manual request → entity mapping for users (spec 10.2). Reads go DB → DTO by projection in the repository.</summary>
public static class UserMapping
{
    /// <summary>E-mail is compared and stored lower-case and trimmed.</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>Requested roles + Employee (always, spec 2.3), without duplicates, ordered.</summary>
    public static IReadOnlyList<Role> NormalizeRoles(IEnumerable<Role>? roles) =>
        (roles ?? []).Append(Role.Employee).Distinct().Order().ToList();

    public static User ToNewUser(this CreateUserRequest request, int organizationId, string employeeCode,
        string passwordHash, IReadOnlyList<Role> roles, DateTime now) => new()
    {
        OrganizationId = organizationId,
        DepartmentId = request.DepartmentId,
        EmployeeCode = employeeCode,
        FirstName = request.FirstName.Trim(),
        LastName = request.LastName.Trim(),
        Email = NormalizeEmail(request.Email),
        JobTitle = request.JobTitle.Trim(),
        PasswordHash = passwordHash,
        MustChangePassword = true,
        IsActive = true,
        CreatedAt = now,
        Roles = roles.Select(role => new UserRole { Role = role }).ToList(),
    };

    /// <summary>Copies the editable details (not roles, not the employee code). Returns whether anything changed.</summary>
    public static bool ApplyDetails(this UpdateUserRequest request, User user)
    {
        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        var email = NormalizeEmail(request.Email);
        var jobTitle = request.JobTitle.Trim();

        var changed = user.FirstName != firstName || user.LastName != lastName || user.Email != email
                      || user.JobTitle != jobTitle || user.DepartmentId != request.DepartmentId;

        user.FirstName = firstName;
        user.LastName = lastName;
        user.Email = email;
        user.JobTitle = jobTitle;
        user.DepartmentId = request.DepartmentId;
        return changed;
    }

    /// <summary>Makes the tracked role rows equal to <paramref name="roles"/>. Returns whether anything changed.</summary>
    public static bool SetRoles(this User user, IReadOnlyCollection<Role> roles)
    {
        var toRemove = user.Roles.Where(r => !roles.Contains(r.Role)).ToList();
        var toAdd = roles.Where(role => user.Roles.All(r => r.Role != role)).ToList();

        foreach (var row in toRemove)
        {
            user.Roles.Remove(row);
        }

        foreach (var role in toAdd)
        {
            user.Roles.Add(new UserRole { UserId = user.Id, Role = role });
        }

        return toRemove.Count > 0 || toAdd.Count > 0;
    }

    public static IReadOnlyList<Role> RoleList(this User user) => user.Roles.Select(r => r.Role).Order().ToList();
}
