namespace Common.Models;

public class Department
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? ManagerUserId { get; set; }
    public bool IsActive { get; set; }

    public Organization Organization { get; set; } = null!;
    public User? Manager { get; set; }
    public ICollection<User> Users { get; set; } = [];
}
