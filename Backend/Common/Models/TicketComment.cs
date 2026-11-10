namespace Common.Models;

public class TicketComment
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int AuthorUserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public bool IsResolution { get; set; }
    public DateTime CreatedAt { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public User AuthorUser { get; set; } = null!;
    public ICollection<TicketAttachment> Attachments { get; set; } = [];
}
