using Common.DTOs.Tickets;
using Common.Enums;
using Common.Models;

namespace Services.Mapping;

internal static class TicketMapping
{
    /// <summary>
    /// A new ticket in status New, not yet classified or routed (spec 3.1). The requester, the department snapshot and
    /// the organization are passed in by the service (from the token and the database) – never from the request.
    /// </summary>
    public static Ticket ToNewTicket(this CreateTicketRequest request, int organizationId, int requesterId,
        int requesterDepartmentId, DateTime now)
    {
        var subject = request.Subject?.Trim();
        return new Ticket
        {
            OrganizationId = organizationId,
            RequesterId = requesterId,
            RequesterDepartmentId = requesterDepartmentId,
            Subject = string.IsNullOrEmpty(subject) ? null : subject,
            Description = request.Description.Trim(),
            Status = TicketStatus.New,
            AiStatus = AiStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>A comment by the signed-in user. Never a resolution reply here (task 3.5 sends those).</summary>
    public static TicketComment ToNewComment(this CreateCommentRequest request, int ticketId, int authorUserId,
        DateTime now) => new()
    {
        TicketId = ticketId,
        AuthorUserId = authorUserId,
        Body = request.Body.Trim(),
        IsInternal = request.IsInternal,
        IsResolution = false,
        CreatedAt = now,
    };
}
