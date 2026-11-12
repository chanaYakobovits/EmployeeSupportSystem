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
}
