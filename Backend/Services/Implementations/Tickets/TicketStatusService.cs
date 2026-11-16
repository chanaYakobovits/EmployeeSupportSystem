using Common.DTOs.Tickets;
using Common.Enums;
using Common.Exceptions;
using Common.Interfaces;
using Common.Models;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Repositories.Interfaces;
using Services.Implementations.Routing;
using Services.Interfaces;
using Services.Validators;
using ValidationException = Common.Exceptions.ValidationException;

namespace Services.Implementations.Tickets;

/// <summary>
/// PATCH /status (task 3.5): the handler's start / request info / resolve and the requester's cancel / confirm / reopen.
/// Who: the requester by <see cref="TicketPermissions.IsRequesterView"/>; a handler in scope by
/// <see cref="TicketPermissions.CanChangeStatus"/>, and then only the personal handler – except "התחל טיפול" on a ticket
/// in the queue of their department, which assigns them (spec 3.3). A department manager or the executive → 403
/// (spec 14 question 8). Logs carry ids and the transition – never the message or the reason (spec 6.4).
/// </summary>
public sealed class TicketStatusService(
    ITicketAccess ticketAccess,
    ITicketRepository tickets,
    IUserRepository users,
    IReferenceDataCache cache,
    ISlaPolicyRepository slaPolicies,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    IValidator<ChangeTicketStatusRequest> validator,
    ILogger<TicketStatusService> logger) : ITicketStatusService
{
    public const string NotAllowedTitle = "אין לך הרשאה לשנות את הסטטוס של פנייה זו";
    public const string ReasonNotAllowedMessage = "נימוק פנימי נכתב רק על ידי המטפל. את ההודעה למטפל יש לכתוב בשדה ההודעה";
    public const string AssignedToOtherTitle = "הפנייה משויכת למטפל אחר";
    public const string NotAssignedTitle = "יש לקחת את הפנייה לטיפולך לפני שינוי הסטטוס";
    public const string ReopenExpiredTitle = "עברו 7 ימים מהפתרון ולא ניתן לפתוח את הפנייה מחדש";
    public const string ReopenExpiredDetail = "ניתן לפתוח פנייה חדשה.";

    private readonly TicketUpdateSteps _steps = new(tickets, unitOfWork, logger);

    public async Task<TicketUpdatedDto> ChangeStatusAsync(int ticketId, ChangeTicketStatusRequest request,
        CancellationToken cancellationToken)
    {
        await validator.EnsureValidAsync(request, cancellationToken);
        var to = request.NewStatus!.Value;

        var access = await ticketAccess.GetAsync(ticketId, cancellationToken);
        var actor = access.Permissions.IsRequesterView ? StatusActor.Requester
            : access.Permissions.CanChangeStatus ? StatusActor.Handler
            : throw new ForbiddenException(NotAllowedTitle, errorType: "status-change-not-allowed");

        // The reason is internal (staff timeline only): the requester would never see what they wrote.
        if (actor == StatusActor.Requester && !string.IsNullOrWhiteSpace(request.Reason))
        {
            throw ValidationException.ForField("reason", ReasonNotAllowedMessage);
        }

        var ticket = await _steps.LoadAsync(access, request.RowVersion!, cancellationToken);
        var from = ticket.Status;
        var transition = TicketStatusRules.Find(from, to, actor) ?? throw TicketStatusTransitions.Invalid(from, to);

        var now = clock.UtcNow;
        IReadOnlyDictionary<Urgency, int>? slaHours = null;
        if (actor == StatusActor.Handler)
        {
            EnsurePersonalHandler(ticket, transition, access);
            if (ticket.AssignedToUserId is null)
            {
                // "התחל טיפול" from the queue: the handler who starts is assigned (spec 3.3) – recorded like "קח לטיפולי".
                Take(ticket, access.UserId, now);
            }
        }
        else if (transition.Action == Common.Constants.TicketActions.Reopen)
        {
            if (!TicketStatusRules.CanReopen(ticket.ResolvedAt, now))
            {
                throw new BusinessRuleException(ReopenExpiredTitle, ReopenExpiredDetail, "reopen-window-expired");
            }

            slaHours = await SlaCalculator.GetHoursAsync(cache, slaPolicies, ticket.OrganizationId, cancellationToken);
            await ReleaseInactiveHandlerAsync(ticket, cancellationToken);
        }

        AddMessage(ticket, request.Message, to, access.UserId, now);
        TicketStatusTransitions.Apply(ticket, to, actor, access.UserId,
            actor == StatusActor.Handler ? request.Reason : null, now, slaHours);

        await _steps.SaveAsync(ticket, $"status {from}->{to}", access.UserId, cancellationToken);

        logger.LogInformation("Ticket {TicketId} status {FromStatus} -> {ToStatus} by user {UserId} ({Actor})",
            ticket.Id, from, to, access.UserId, actor);
        return TicketUpdateSteps.Result(ticket, access);
    }

    /// <summary>
    /// The personal handler only (decision in CLAUDE.md); "התחל טיפול" also from the queue of the user's department.
    /// A colleague takes the ticket first (POST /assign).
    /// </summary>
    private static void EnsurePersonalHandler(Ticket ticket, StatusTransition transition, TicketAccessResult access)
    {
        if (ticket.AssignedToUserId == access.UserId)
        {
            return;
        }

        if (ticket.AssignedToUserId is not null)
        {
            throw new BusinessRuleException(AssignedToOtherTitle, errorType: "ticket-assigned-to-other");
        }

        var inMyQueue = access.Viewer.Roles.Contains(Role.Handler) && ticket.AssignedDepartmentId is not null
                        && ticket.AssignedDepartmentId == access.Viewer.DepartmentId;
        if (transition.Action != Common.Constants.TicketActions.Start || !inMyQueue)
        {
            throw new BusinessRuleException(NotAssignedTitle, errorType: "ticket-not-assigned");
        }
    }

    private static void Take(Ticket ticket, int userId, DateTime now)
    {
        ticket.AssignedToUserId = userId;
        ticket.Changes.Add(new TicketChange
        {
            Kind = TicketChangeKind.Taken,
            ChangedByUserId = userId,
            ChangedAt = now,
            FromCategoryId = ticket.CategoryId,
            ToCategoryId = ticket.CategoryId,
            FromUrgency = ticket.Urgency,
            ToUrgency = ticket.Urgency,
            FromDepartmentId = ticket.AssignedDepartmentId,
            ToDepartmentId = ticket.AssignedDepartmentId,
            FromUserId = null,
            ToUserId = userId,
        });
    }

    /// <summary>
    /// A reopened ticket goes back to its handler – unless they are no longer an active handler of the handling
    /// department (deactivation leaves resolved tickets with them): then it waits in the department's queue.
    /// </summary>
    private async Task ReleaseInactiveHandlerAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        if (ticket.AssignedToUserId is not { } handlerId || ticket.AssignedDepartmentId is not { } departmentId)
        {
            return;
        }

        var candidates = await users.GetHandlerCandidatesAsync([departmentId], handlerId, cancellationToken);
        if (!candidates.Any(c => c.UserId == handlerId && c.DepartmentId == departmentId))
        {
            ticket.AssignedToUserId = null;
            logger.LogInformation("Reopened ticket {TicketId}: handler {UserId} is no longer active, back to the queue",
                ticket.Id, handlerId);
        }
    }

    /// <summary>The message to the employee, as a visible comment saved with the change; the resolution reply is marked.</summary>
    private static void AddMessage(Ticket ticket, string? message, TicketStatus to, int authorUserId, DateTime now)
    {
        var body = message?.Trim();
        if (string.IsNullOrEmpty(body))
        {
            return;
        }

        ticket.Comments.Add(new TicketComment
        {
            AuthorUserId = authorUserId,
            Body = body,
            IsInternal = false,
            IsResolution = to == TicketStatus.Resolved,
            CreatedAt = now,
        });
    }
}
