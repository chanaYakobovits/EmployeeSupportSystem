namespace Common.Models;

public class Department
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? ManagerUserId { get; set; }

    /// <summary>
    /// The departmental HR handler (task 3.3): receives the department's RequesterDepartmentHr tickets (spec 3.5).
    /// An active user with the Handler role, possibly of another department. Null = those tickets go to central HR.
    /// </summary>
    public int? HrUserId { get; set; }

    public bool IsActive { get; set; }

    public Organization Organization { get; set; } = null!;
    public User? Manager { get; set; }
    public User? HrUser { get; set; }
    public ICollection<User> Users { get; set; } = [];
}
