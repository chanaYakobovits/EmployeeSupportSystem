namespace Common.Enums;

/// <summary>User role (spec 2.3). A user may hold several roles; every user is also an Employee.</summary>
public enum Role : byte
{
    Employee = 1,
    Handler = 2,
    DepartmentManager = 3,
    Executive = 4,
    Admin = 5
}
