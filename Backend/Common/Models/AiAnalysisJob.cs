using Common.Enums;

namespace Common.Models;

public class AiAnalysisJob
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public AiJobStatus Status { get; set; }
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime? LockedUntil { get; set; }

    /// <summary>Error code only (e.g. Timeout, RateLimited, InvalidResponse), never request or response content.</summary>
    public string? LastErrorCode { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Ticket Ticket { get; set; } = null!;
}
