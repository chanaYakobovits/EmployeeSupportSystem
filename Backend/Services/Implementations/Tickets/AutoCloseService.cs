using Common.DTOs.Tickets;
using Common.Enums;
using Common.Exceptions;
using Common.Interfaces;
using Microsoft.Extensions.Logging;
using Repositories.Interfaces;
using Services.Interfaces;

namespace Services.Implementations.Tickets;

/// <inheritdoc />
/// <remarks>
/// Each ticket is closed on its own (its own save): a conflict with the employee's action on one ticket skips only that
/// one. The close is conditioned on the version just read, so an employee who reopens or confirms at the same moment
/// wins and the job skips quietly.
/// </remarks>
public sealed class AutoCloseService(
    ITicketRepository tickets,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<AutoCloseService> logger) : IAutoCloseService
{
    public Task<IReadOnlyList<TicketRef>> GetDueAsync(int batchSize, CancellationToken cancellationToken) =>
        tickets.GetResolvedDueAsync(TicketStatusRules.AutoCloseCutoff(clock.UtcNow), batchSize, cancellationToken);

    public async Task<bool> TryCloseAsync(int ticketId, CancellationToken cancellationToken)
    {
        var ticket = await tickets.FindForUpdateAsync(ticketId, cancellationToken);
        var now = clock.UtcNow;
        if (ticket is null || ticket.Status != TicketStatus.Resolved
                           || !TicketStatusRules.IsAutoCloseDue(ticket.ResolvedAt, now))
        {
            return false;
        }

        tickets.ExpectRowVersion(ticket, ticket.RowVersion);
        TicketStatusTransitions.Apply(ticket, TicketStatus.Closed, StatusActor.System, null, null, now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictException exception) when (exception.ErrorType == "concurrency")
        {
            logger.LogInformation("Auto-close of ticket {TicketId} skipped: changed meanwhile", ticketId);
            return false;
        }

        logger.LogInformation("Ticket {TicketId} closed automatically ({Days} days after the resolution)",
            ticketId, TicketStatusRules.ResolvedWindow.TotalDays);
        return true;
    }
}
