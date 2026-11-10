using Common.Enums;

namespace Common.Models;

public class TicketStatusHistory
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public TicketStatus? PreviousStatus { get; set; }
    public TicketStatus NewStatus { get; set; }

    /// <summary>Null when the change was made by the system.</summary>
    public int? ChangedByUserId { get; set; }

    public DateTime ChangedAt { get; set; }
    public string? Reason { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public User? ChangedByUser { get; set; }
}
