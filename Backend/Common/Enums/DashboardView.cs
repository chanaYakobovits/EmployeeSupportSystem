namespace Common.Enums;

/// <summary>Which tickets a department dashboard counts (spec 4.5, 9.3).</summary>
public enum DashboardView
{
    /// <summary>"פניות של עובדי המחלקה": tickets opened by the department's employees (RequesterDepartmentId).</summary>
    Requesters = 0,

    /// <summary>"פניות בטיפול המחלקה": tickets the department handles (AssignedDepartmentId).</summary>
    Handling = 1,
}
