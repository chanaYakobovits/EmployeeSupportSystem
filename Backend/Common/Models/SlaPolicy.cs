using Common.Enums;

namespace Common.Models;

public class SlaPolicy
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public Urgency Urgency { get; set; }
    public int ResolutionHours { get; set; }

    public Organization Organization { get; set; } = null!;
}
