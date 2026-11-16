using Common.DTOs.Tickets;

namespace Services.Interfaces;

/// <summary>
/// Bulk update of tickets – task 3.6 (spec 7.6, 11.2): up to 100 tickets, Partial Success. Each ticket goes through the
/// single action (<see cref="ITicketStatusService"/>, <see cref="ITicketHandlingService"/>) with its own save; the
/// response tells which succeeded and why the others did not.
/// </summary>
public interface ITicketBulkService
{
    /// <summary>"התחל טיפול" on every ticket (Assigned → InProgress, from the queue too).</summary>
    /// <exception cref="Common.Exceptions.ValidationException">Invalid request (another status, 0 or over 100, a repeated id).</exception>
    Task<BulkUpdateResultDto> ChangeStatusAsync(BulkChangeStatusRequest request, string ipAddress,
        CancellationToken cancellationToken);

    /// <summary>Transfers every ticket to the same handler or department queue, with the same reason.</summary>
    /// <exception cref="Common.Exceptions.ValidationException">Invalid request (target, reason, items).</exception>
    Task<BulkUpdateResultDto> AssignAsync(BulkAssignRequest request, string ipAddress,
        CancellationToken cancellationToken);
}
