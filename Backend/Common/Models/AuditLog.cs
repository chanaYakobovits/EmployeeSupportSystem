namespace Common.Models;

/// <summary>Audit record of a sensitive action (spec 6.4). Deliberately has no content fields.</summary>
public class AuditLog
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Organization Organization { get; set; } = null!;
    public User? User { get; set; }
}
