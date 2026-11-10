namespace Common.Models;

public class Organization
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Department> Departments { get; set; } = [];
    public ICollection<User> Users { get; set; } = [];
}
