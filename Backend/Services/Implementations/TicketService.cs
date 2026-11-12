using Common.DTOs.Paging;
using Common.DTOs.Tickets;
using Common.Enums;
using Common.Exceptions;
using Common.Interfaces;
using Common.Models;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Repositories.Interfaces;
using Services.Interfaces;
using Services.Mapping;
using Services.Validators;

namespace Services.Implementations;

/// <summary>
/// Tickets – task 3.1: opening a ticket and the requester's own view. The requester is always the signed-in user
/// (spec 6.2). Logs carry ticket and user ids only – never the subject or description (spec 6.4).
/// </summary>
public sealed class TicketService(
    ITicketRepository tickets,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ICurrentUser currentUser,
    IValidator<CreateTicketRequest> createValidator,
    IValidator<MyTicketsQuery> listValidator,
    ILogger<TicketService> logger) : ITicketService
{
    public const string TicketNotFoundTitle = "הפנייה לא נמצאה";

    // ===================== Create (spec 4.3 – what the employee experiences) =====================

    public async Task<TicketCreatedDto> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken)
    {
        await createValidator.EnsureValidAsync(request, cancellationToken);

        var requesterId = currentUser.UserId;
        // Snapshot of the department at this moment (spec 3.1), from the database: the token's claim may be stale.
        var departmentId = await users.GetActiveDepartmentIdAsync(requesterId, cancellationToken)
                           ?? throw new AuthenticationFailedException(AuthenticationFailedException.NotSignedInTitle,
                               errorType: "unauthorized");

        var now = clock.UtcNow;
        var ticket = request.ToNewTicket(currentUser.OrganizationId, requesterId, departmentId, now);
        StageNewTicket(ticket, now);

        // One SaveChanges = one transaction for the ticket and every row staged with it (spec 4.3: the ticket and its
        // analysis job are never saved apart).
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Ticket {TicketId} created by user {UserId}", ticket.Id, requesterId);
        return new TicketCreatedDto(ticket.Id);
    }

    /// <summary>
    /// Everything saved together with a new ticket. Later tasks add their rows here, in the same transaction:
    /// routing and SLA (3.3) and the AI analysis job (4.3).
    /// </summary>
    private void StageNewTicket(Ticket ticket, DateTime now)
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

    public async Task<RequesterTicketDetailsDto> GetAsync(int ticketId, CancellationToken cancellationToken)
    {
        // Viewing scopes, each filtered in its own query. Task 3.4 adds the staff scopes (handler, department manager,
        // executive) before this one, with a staff view of the ticket.
        var asRequester = await tickets.GetForRequesterAsync(ticketId, currentUser.UserId, cancellationToken);
        return asRequester ?? throw new NotFoundException(TicketNotFoundTitle);
    }
}
