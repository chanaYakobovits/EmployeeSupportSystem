using Common.Enums;

namespace Common.Models;

public class Ticket
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public int RequesterId { get; set; }

    /// <summary>Snapshot of the requester's department at creation time; never changes afterwards.</summary>
    public int RequesterDepartmentId { get; set; }

    public string? Subject { get; set; }
    public string Description { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public Urgency? Urgency { get; set; }
    public TicketStatus Status { get; set; }
    public AiStatus AiStatus { get; set; }

    /// <summary>Null until the ticket is classified (by AI, fallback or a handler).</summary>
    public ClassificationSource? ClassificationSource { get; set; }

    public int? AssignedDepartmentId { get; set; }
    public int? AssignedToUserId { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    /// <summary>SQL Server rowversion, used for optimistic concurrency (spec 11.2).</summary>
    public byte[] RowVersion { get; set; } = [];

    public Organization Organization { get; set; } = null!;
    public User Requester { get; set; } = null!;
    public Department RequesterDepartment { get; set; } = null!;
    public Category? Category { get; set; }
    public Department? AssignedDepartment { get; set; }
    public User? AssignedToUser { get; set; }
    public TicketAiAnalysis? AiAnalysis { get; set; }
    public ICollection<AiAnalysisJob> AiAnalysisJobs { get; set; } = [];
    public ICollection<TicketStatusHistory> StatusHistory { get; set; } = [];
    public ICollection<TicketComment> Comments { get; set; } = [];
    public ICollection<TicketAttachment> Attachments { get; set; } = [];
}
