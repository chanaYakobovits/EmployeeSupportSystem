using System.Diagnostics;
using Common.Constants;
using Common.DTOs.Tickets;
using Common.Interfaces;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Repositories.Interfaces;
using Services.Interfaces;
using Services.Validators;

namespace Services.Implementations.Tickets;

/// <inheritdoc />
/// <remarks>
/// No rule of its own: every ticket goes through the single action – TicketAccess, TicketStatusRules, routing, history,
/// the version check and the per-ticket log – and what it throws becomes the ticket's reason (<see cref="BulkFailures"/>).
/// Each ticket is saved on its own; between tickets the scope is cleared (<see cref="IUnitOfWork.DiscardChanges"/>), so a
/// ticket whose save failed is not written with the next one. Logs carry ids, reasons and counts – never the reason text.
/// </remarks>
public sealed class TicketBulkService(
    ITicketStatusService statusService,
    ITicketHandlingService handlingService,
    IAuditLogRepository auditLogs,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IValidator<BulkChangeStatusRequest> statusValidator,
    IValidator<BulkAssignRequest> assignValidator,
    ILogger<TicketBulkService> logger) : ITicketBulkService
{
    public async Task<BulkUpdateResultDto> ChangeStatusAsync(BulkChangeStatusRequest request, string ipAddress,
        CancellationToken cancellationToken)
    {
        await statusValidator.EnsureValidAsync(request, cancellationToken);
        var newStatus = request.NewStatus!.Value;

        return await RunAsync("status", AuditActions.TicketBulkStatus, request.Items!, ipAddress,
            (item, ct) => statusService.ChangeStatusAsync(item.Id!.Value,
                new ChangeTicketStatusRequest(newStatus, item.RowVersion), ct),
            cancellationToken);
    }

    public async Task<BulkUpdateResultDto> AssignAsync(BulkAssignRequest request, string ipAddress,
        CancellationToken cancellationToken)
    {
        await assignValidator.EnsureValidAsync(request, cancellationToken);

        return await RunAsync("assign", AuditActions.TicketBulkAssign, request.Items!, ipAddress,
            (item, ct) => handlingService.AssignAsync(item.Id!.Value,
                new AssignTicketRequest(request.TargetUserId, request.TargetDepartmentId, request.Reason,
                    item.RowVersion), ct),
            cancellationToken);
    }

    private async Task<BulkUpdateResultDto> RunAsync(string action, string auditAction,
        IReadOnlyList<BulkTicketItem> items, string ipAddress, Func<BulkTicketItem, CancellationToken, Task> single,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var succeeded = new List<int>(items.Count);
        var failed = new List<BulkFailureDto>();

        foreach (var item in items)
        {
            var ticketId = item.Id!.Value;
            try
            {
                await single(item, cancellationToken);
                succeeded.Add(ticketId);
            }
            catch (Exception exception) when (BulkFailures.Classify(ticketId, exception) is { } failure)
            {
                failed.Add(failure);
                logger.LogInformation("Bulk {Action}: ticket {TicketId} not updated by user {UserId} ({Reason}, {ErrorType})",
                    action, ticketId, currentUser.UserId, failure.Reason, failure.ErrorType);
            }
            finally
            {
                // Whatever happened, the next ticket starts from a clean scope.
                unitOfWork.DiscardChanges();
            }
        }

        auditLogs.Add(AuditEntries.ForEntity(auditAction, AuditEntityTypes.Ticket, null, currentUser.OrganizationId,
            currentUser.UserId, ipAddress, clock.UtcNow));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Bulk {Action} by user {UserId}: {Requested} requested, {Succeeded} succeeded, {Failed} failed in {ElapsedMs} ms",
            action, currentUser.UserId, items.Count, succeeded.Count, failed.Count, stopwatch.ElapsedMilliseconds);

        return new BulkUpdateResultDto(succeeded, failed);
    }
}
