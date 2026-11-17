using Common.Enums;

namespace Common.Models;

/// <summary>
/// Queue row "analyze ticket X" (spec 5.2), written in the same transaction as the ticket. Holds no content.
/// </summary>
public class AiAnalysisJob
{
    public int Id { get; set; }

    /// <summary>
    /// The ticket's organization. The worker claims jobs of every organization (no query filter on this table, like
    /// <see cref="InvitationEmailJob"/>) and then processes each one in a scope bound to this organization.
    /// </summary>
    public int OrganizationId { get; set; }

    public int TicketId { get; set; }
    public AiJobStatus Status { get; set; }
    /// <summary>
    /// Claims so far (each claim adds one, also one that crashed). Concurrency token: a job re-claimed by another server
    /// after its lock expired has a higher value, so the first server's save fails instead of writing a second result.
    /// </summary>
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime? LockedUntil { get; set; }

    /// <summary>Error code only (e.g. Timeout, RateLimited, InvalidResponse), never request or response content.</summary>
    public string? LastErrorCode { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Ticket Ticket { get; set; } = null!;
}
