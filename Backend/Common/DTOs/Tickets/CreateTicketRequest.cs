namespace Common.DTOs.Tickets;

/// <summary>
/// POST /api/tickets (task 3.1; attachments come in 3.2). Deliberately has no requester or department field:
/// both come from the signed-in user (spec 3.1, 6.2). Unknown JSON properties (e.g. "requesterId") are ignored.
/// </summary>
public sealed class CreateTicketRequest
{
    /// <summary>Optional, up to 150 characters.</summary>
    public string? Subject { get; set; }

    /// <summary>Required, 10–4,000 characters (after trimming).</summary>
    public string Description { get; set; } = string.Empty;
}
