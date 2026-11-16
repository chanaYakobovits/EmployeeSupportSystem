using Common.Enums;

namespace Common.Models;

/// <summary>
/// A change of a ticket's classification or assignment made by staff, with its reason (task 3.4, spec 4.4: "נשמר
/// בהיסטוריה"). Shown only in the staff timeline: a separate table rather than TicketStatusHistory, which the
/// requester's queries read – what the requester may not see is never in any of their queries.
/// The From/To columns hold what changed; the ones that did not change are equal (or both null).
/// </summary>
public class TicketChange
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public TicketChangeKind Kind { get; set; }
    public int ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; }

    /// <summary>Required, except when a handler takes a ticket from the queue.</summary>
    public string? Reason { get; set; }

    public int? FromCategoryId { get; set; }
    public int? ToCategoryId { get; set; }
    public Urgency? FromUrgency { get; set; }
    public Urgency? ToUrgency { get; set; }
    public int? FromDepartmentId { get; set; }
    public int? ToDepartmentId { get; set; }
    public int? FromUserId { get; set; }
    public int? ToUserId { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public User ChangedByUser { get; set; } = null!;
    public Category? FromCategory { get; set; }
    public Category? ToCategory { get; set; }
    public Department? FromDepartment { get; set; }
    public Department? ToDepartment { get; set; }
    public User? FromUser { get; set; }
    public User? ToUser { get; set; }
}
