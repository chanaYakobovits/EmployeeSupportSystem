namespace Repositories.Interfaces;

/// <summary>
/// Database names of the unique indexes the services translate to field errors when two requests race past
/// their pre-checks (<see cref="Common.Exceptions.UniqueConstraintException.IndexName"/>).
/// </summary>
public static class UniqueIndexes
{
    public const string UserEmail = "IX_Users_Email";
    public const string UserEmployeeCode = "IX_Users_EmployeeCode";
    public const string DepartmentName = "IX_Departments_OrganizationId_Name";
}
