using Common.Enums;

namespace Common.Models;

public class Category
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RoutingType RoutingType { get; set; }
    public int? HandlingDepartmentId { get; set; }
    public bool IsFallback { get; set; }
    public bool IsActive { get; set; }

    public Organization Organization { get; set; } = null!;
    public Department? HandlingDepartment { get; set; }
}
