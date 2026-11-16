using System.Diagnostics;
using Common.Constants;
using Common.DTOs.Paging;
using Common.DTOs.Routing;
using Common.DTOs.Tickets;
using Common.Enums;
using Common.Exceptions;
using Common.Interfaces;
using Common.Models;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Repositories.Interfaces;
using Services.Implementations.Files;
using Services.Implementations.Tickets;
using Services.Interfaces;
using Services.Mapping;
using Services.Validators;

namespace Services.Implementations;

/// <summary>
/// Tickets – task 3.1: opening a ticket (with attachments since 3.2) and the requester's own view. The requester is
/// always the signed-in user (spec 6.2). Logs carry ids, sizes and types only – never the subject, description or a file
/// name (spec 6.4).
/// </summary>
public sealed class TicketService(
    ITicketRepository tickets,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ITicketAccess ticketAccess,
    IRoutingService routing,
    ITicketClassifier classifier,
    IFileStorage storage,
    IDateTimeProvider clock,
    ICurrentUser currentUser,
    IValidator<CreateTicketRequest> createValidator,
    IValidator<IReadOnlyList<IncomingFile>> filesValidator,
    IValidator<MyTicketsQuery> listValidator,
    IValidator<StaffTicketsQuery> staffListValidator,
    IAuditLogRepository auditLogs,
    ILogger<TicketService> logger) : ITicketService
{
    public const string TicketNotFoundTitle = TicketAccess.TicketNotFoundTitle;

    // ===================== Create (spec 4.3 – what the employee experiences) =====================

    public async Task<TicketCreatedDto> CreateAsync(CreateTicketRequest request, IReadOnlyList<IncomingFile> files,
        CancellationToken cancellationToken)
    {
        await createValidator.EnsureValidAsync(request, cancellationToken);
        // Every file is checked (count, size, type by content) before anything is written.
        await filesValidator.EnsureValidAsync(files, cancellationToken);

        var requesterId = currentUser.UserId;
        // Snapshot of the department at this moment (spec 3.1), from the database: the token's claim may be stale.
        var departmentId = await users.GetActiveDepartmentIdAsync(requesterId, cancellationToken)
                           ?? throw new AuthenticationFailedException(AuthenticationFailedException.NotSignedInTitle,
                               errorType: "unauthorized");

        var now = clock.UtcNow;
        var ticket = request.ToNewTicket(currentUser.OrganizationId, requesterId, departmentId, now);

        // Written to disk first; deleted again if the database save below fails or is cancelled.
        await using var staged = new StagedFiles(storage, logger);
        foreach (var file in files)
        {
            // Files of the new-ticket form have no comment: visible to the requester and the staff.
            ticket.Attachments.Add(await staged.SaveAttachmentAsync(file, requesterId, now, cancellationToken));
        }

        var routed = await StageNewTicketAsync(ticket, now, cancellationToken);

        // One SaveChanges = one transaction for the ticket and every row staged with it: routing, SLA and the history
        // (spec 4.3: the ticket and its analysis job are never saved apart).
        await unitOfWork.SaveChangesAsync(cancellationToken);
        staged.Commit();

        logger.LogInformation("Ticket {TicketId} created by user {UserId} with {AttachmentCount} attachments",
            ticket.Id, requesterId, ticket.Attachments.Count);
        if (routed is not null)
        {
            RoutingLog.Routed(logger, ticket.Id, routed, ClassificationSource.Fallback);
        }
        foreach (var attachment in ticket.Attachments)
        {
            logger.LogInformation("Attachment {AttachmentId} added to ticket {TicketId} ({SizeBytes} bytes, {ContentType})",
                attachment.Id, ticket.Id, attachment.SizeBytes, attachment.ContentType);
        }

        return new TicketCreatedDto(ticket.Id);
    }

    /// <summary>
    /// Everything saved together with a new ticket, in the same transaction: the opening history row, and the
    /// fallback classification with its routing and SLA (task 3.3: no AI yet, so every ticket goes to GENERAL right
    /// away, AiStatus Skipped). Task 4.3 adds the AI analysis job here, and keeps this path for Ai:Enabled = false.
    /// The AI job of 4.3 analyzes only the redacted description – attachments are never sent to the AI (spec 4.3, 5.1):
    /// not their content, not their names.
    /// </summary>
    /// <returns>The routing decision, to log after the save; null if the ticket could not be routed.</returns>
    private async Task<RoutingDecision?> StageNewTicketAsync(Ticket ticket, DateTime now,
        CancellationToken cancellationToken)
    {
        // The opening entry of the timeline (spec 3.3: every transition is recorded).
        ticket.StatusHistory.Add(new TicketStatusHistory
        {
            PreviousStatus = null,
            NewStatus = TicketStatus.New,
            ChangedByUserId = ticket.RequesterId,
            ChangedAt = now,
        });
        tickets.Add(ticket);

        ticket.AiStatus = AiStatus.Skipped;
        var fallbackId = await routing.GetFallbackCategoryIdAsync(ticket.OrganizationId, cancellationToken);
        if (fallbackId is not { } categoryId)
        {
            // Cannot happen with valid data (one fallback per organization, never deactivated). The employee's ticket is
            // still saved – New, unrouted – rather than failing the request.
            logger.LogError("Organization {OrganizationId} has no active fallback category; a new ticket stays unrouted",
                ticket.OrganizationId);
            return null;
        }

        // Urgency null until a handler sets it (decision in CLAUDE.md); the SLA then uses Medium (spec 3.4).
        return await classifier.ApplyAsync(ticket,
            new TicketClassification(categoryId, Urgency: null, ClassificationSource.Fallback), now, cancellationToken);
    }

    // ===================== The requester's view (spec 7.3, 7.5) =====================

    public async Task<PagedResult<RequesterTicketListItemDto>> GetMyPageAsync(MyTicketsQuery query,
        CancellationToken cancellationToken)
    {
        await listValidator.EnsureValidAsync(query, cancellationToken);
        return await tickets.GetRequesterPageAsync(currentUser.UserId, query, cancellationToken);
    }

    public Task<RequesterTicketsSummaryDto> GetMySummaryAsync(CancellationToken cancellationToken) =>
        tickets.GetRequesterSummaryAsync(currentUser.UserId, IsraelTime.StartOfMonthUtc(clock.UtcNow), cancellationToken);

    public async Task<ITicketDetailsDto> GetAsync(int ticketId, string ipAddress, CancellationToken cancellationToken)
    {
        // Any viewing scope (spec 2.4). The requester – even one who is staff too – gets the requester's DTO.
        var access = await ticketAccess.GetAsync(ticketId, cancellationToken);
        if (access.Permissions.IsRequesterView)
        {
            return await tickets.GetDetailsAsync(ticketId, cancellationToken)
                   ?? throw new NotFoundException(TicketNotFoundTitle);
        }

        var now = clock.UtcNow;
        var allowedActions = TicketAllowedActions.For(access.Permissions, access.Ticket, access.Viewer);
        var details = await tickets.GetStaffDetailsAsync(ticketId, allowedActions, now, cancellationToken)
                      ?? throw new NotFoundException(TicketNotFoundTitle);

        // Spec 6.4: viewing another employee's ticket – every staff view, after the ticket was actually read.
        auditLogs.Add(AuditEntries.ForEntity(AuditActions.TicketViewed, AuditEntityTypes.Ticket, ticketId,
            currentUser.OrganizationId, access.UserId, ipAddress, now));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return details;
    }

    // ===================== The staff lists (task 3.4, spec 7.6) =====================

    public async Task<PagedResult<StaffTicketListItemDto>> GetStaffPageAsync(StaffTicketsQuery query,
        CancellationToken cancellationToken)
    {
        await staffListValidator.EnsureValidAsync(query, cancellationToken);
        var scope = query.Scope!.Value;
        var predicate = await ticketAccess.GetListScopeAsync(scope, cancellationToken);

        // Calendar days of Israel (spec 11.6); "to" includes its whole day.
        var fromUtc = query.From is { } from ? IsraelTime.StartOfDayUtc(from) : (DateTime?)null;
        var toUtc = query.To is { } to ? IsraelTime.StartOfDayUtc(to.AddDays(1)) : (DateTime?)null;

        var started = Stopwatch.GetTimestamp();
        var page = await tickets.GetStaffPageAsync(predicate, query, fromUtc, toUtc, clock.UtcNow, cancellationToken);

        // Ids, the scope and the result only – never the search text (it may hold a name or a subject).
        logger.LogInformation(
            "Staff ticket list {Scope} for user {UserId}: {TotalCount} tickets, page {Page} in {ElapsedMs:0} ms",
            scope, currentUser.UserId, page.TotalCount, page.Page, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return page;
    }
}
