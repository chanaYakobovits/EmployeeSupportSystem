using Common.Enums;
using Common.Exceptions;

namespace Services.Implementations;

/// <summary>
/// The department-manager rule, in one place (CLAUDE.md, same as DatabaseSeeder.ValidateManagerRoles):
/// <list type="bullet">
/// <item>The manager of a department (Department.ManagerUserId) holds DepartmentManager – or Executive, which covers it
/// (the CEO manages the Management department).</item>
/// <item>Only a department manager holds DepartmentManager.</item>
/// <item>A user manages at most one department.</item>
/// </list>
/// The role is therefore derived from the departments screen: the users screen cannot add or remove it.
/// </summary>
public static class DepartmentManagerRules
{
    public const string RoleOnlyThroughDepartmentsMessage =
        "התפקיד 'מנהל מחלקה' ניתן רק דרך הגדרת מנהל במסך המחלקות";

    /// <summary>Whether a manager with these roles must also get the DepartmentManager role.</summary>
    public static bool NeedsDepartmentManagerRole(IReadOnlyCollection<Role> roles) =>
        !roles.Contains(Role.Executive) && !roles.Contains(Role.DepartmentManager);

    /// <summary>Checks the roles requested on the users screen against the department the user manages.</summary>
    /// <param name="managedDepartmentName">The department the user manages, or null.</param>
    /// <exception cref="BusinessRuleException">The requested roles break the rule (→ 422).</exception>
    public static void EnsureConsistent(IReadOnlyCollection<Role> requestedRoles, string? managedDepartmentName)
    {
        if (managedDepartmentName is null && requestedRoles.Contains(Role.DepartmentManager))
        {
            throw new BusinessRuleException(RoleOnlyThroughDepartmentsMessage,
                "העובד אינו מוגדר כמנהל של אף מחלקה.", "department-manager-role");
        }

        if (managedDepartmentName is not null
            && !requestedRoles.Contains(Role.DepartmentManager)
            && !requestedRoles.Contains(Role.Executive))
        {
            throw new BusinessRuleException(
                $"העובד מנהל את מחלקת {managedDepartmentName} ולכן חייב להחזיק בתפקיד 'מנהל מחלקה'",
                "כדי להסיר את התפקיד יש להחליף קודם את מנהל המחלקה במסך המחלקות.", "department-manager-role");
        }
    }

    /// <exception cref="BusinessRuleException">The user already manages another department (→ 422).</exception>
    public static void EnsureManagesNoOtherDepartment(string? otherManagedDepartmentName)
    {
        if (otherManagedDepartmentName is not null)
        {
            throw new BusinessRuleException($"העובד כבר מנהל את מחלקת {otherManagedDepartmentName}",
                "עובד יכול לנהל מחלקה אחת בלבד. יש להחליף קודם את המנהל של המחלקה האחרת.", "manages-other-department");
        }
    }
}
