using Common.DTOs.Routing;
using Common.DTOs.Tickets;
using Common.Enums;
using Common.Exceptions;
using Common.Interfaces;
using Common.Models;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Repositories.Interfaces;
using Services.Interfaces;
using Services.Validators;
using ValidationException = Common.Exceptions.ValidationException;

namespace Services.Implementations.Tickets;

/// <summary>
/// Reclassification and transfer (task 3.4). Logs carry ids, the action and its result – never the reason (spec 6.4).
/// <para>
/// When the personal handler changes, an InProgress ticket goes back to Assigned: the new handler has not started yet
/// (decision in CLAUDE.md). WaitingForEmployee stays – it waits for the employee whoever handles it. Taking a ticket
/// from the queue keeps the status ("התחל טיפול" is task 3.5).
/// </para>
/// </summary>
public sealed class TicketHandlingService(
    ITicketAccess ticketAccess,
    ITicketRepository tickets,
    IUserRepository users,
    ICategoryRepository categories,
    IDepartmentRepository departments,
    IReferenceDataCache cache,
    ITicketClassifier classifier,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ICurrentUser currentUser,
    IValidator<ReclassifyTicketRequest> reclassifyValidator,
    IValidator<AssignTicketRequest> assignValidator,
    ILogger<TicketHandlingService> logger) : ITicketHandlingService
{
    public const string ConflictTitle = "הפנייה עודכנה על ידי משתמש אחר";
    public const string ConflictDetail = "יש לרענן את הפנייה ולנסות שוב.";
    public const string ReclassifyNotAllowedTitle = "אין לך הרשאה לשנות את הסיווג של פנייה זו";
    public const string AssignNotAllowedTitle = "אין לך הרשאה להעביר פנייה זו";
    public const string TransferNotAllowedTitle = "מנהל מחלקה יכול להעביר רק בין מטפלי המחלקה";
    public const string NotOpenTitle = "לא ניתן לשנות סיווג או שיוך של פנייה שנפתרה, נסגרה או בוטלה";
    public const string ClassificationUnchangedTitle = "הקטגוריה ורמת הדחיפות לא השתנו";
    public const string AssignmentUnchangedTitle = "הפנייה כבר משויכת ליעד שנבחר";
    public const string AssigneeIsRequesterTitle = "לא ניתן להעביר פנייה לעובד שפתח אותה";
    public const string AssigneeNotHandlerTitle = "ניתן להעביר רק למטפל פעיל במחלקה המטפלת";
    public const string NotRoutedTitle = "הפנייה עדיין לא נותבה למחלקה מטפלת";
    public const string NoHandlersTitle = "במחלקה שנבחרה אין מטפלים פעילים";
    public const string CategoryInvalidMessage = "הקטגוריה אינה קיימת או אינה פעילה";
    public const string CategoryRequiredMessage = "יש לבחור קטגוריה לפנייה שעדיין לא סווגה";
    public const string DepartmentInvalidMessage = "המחלקה אינה קיימת או אינה פעילה";

    // ===================== PATCH /classification =====================

    public async Task<TicketUpdatedDto> ReclassifyAsync(int ticketId, ReclassifyTicketRequest request,
        CancellationToken cancellationToken)
    {
        await reclassifyValidator.EnsureValidAsync(request, cancellationToken);
        var (access, ticket) = await LoadAsync(ticketId, request.RowVersion!, cancellationToken);
        if (!access.Permissions.CanReclassify)
        {
            throw new ForbiddenException(ReclassifyNotAllowedTitle, errorType: "reclassify-not-allowed");
        }

        EnsureOpen(ticket);

        var categoryId = request.CategoryId ?? ticket.CategoryId
                         ?? throw ValidationException.ForField("categoryId", CategoryRequiredMessage);
        if (request.CategoryId is { } requested && !await IsActiveCategoryAsync(ticket.OrganizationId, requested,
                cancellationToken))
        {
            throw ValidationException.ForField("categoryId", CategoryInvalidMessage);
        }

        var urgency = request.Urgency ?? ticket.Urgency;
        if (categoryId == ticket.CategoryId && urgency == ticket.Urgency
            && ticket.ClassificationSource == ClassificationSource.Manual)
        {
            throw new BusinessRuleException(ClassificationUnchangedTitle, errorType: "classification-unchanged");
        }

        var now = clock.UtcNow;
        var change = NewChange(ticket, TicketChangeKind.Reclassified, access.UserId, request.Reason, now);

        // Through the one entry point of every classification (task 3.3): routing, SLA from the opening, New → Assigned.
        var decision = await classifier.ApplyAsync(ticket,
            new TicketClassification(categoryId, urgency, ClassificationSource.Manual), now, cancellationToken);
        OnHandlerChanged(ticket, change.FromUserId, access.UserId, now, keepStatus: false);
        Complete(change, ticket);

        await SaveAsync(ticket, "reclassify", access.UserId, cancellationToken);

        logger.LogInformation(
            "Ticket {TicketId} reclassified by user {UserId}: category {FromCategoryId} -> {ToCategoryId}, urgency {FromUrgency} -> {ToUrgency}",
            ticket.Id, access.UserId, change.FromCategoryId, change.ToCategoryId, change.FromUrgency, change.ToUrgency);
        if (decision is not null)
        {
            RoutingLog.Routed(logger, ticket.Id, decision, ClassificationSource.Manual);
        }

        return Result(ticket, access);
    }

    // ===================== POST /assign =====================

    public async Task<TicketUpdatedDto> AssignAsync(int ticketId, AssignTicketRequest request,
        CancellationToken cancellationToken)
    {
        await assignValidator.EnsureValidAsync(request, cancellationToken);
        var (access, ticket) = await LoadAsync(ticketId, request.RowVersion!, cancellationToken);

        TicketChangeKind kind;
        if (request.TargetUserId is { } targetUserId)
        {
            if (!access.Permissions.CanAssign)
            {
                throw new ForbiddenException(AssignNotAllowedTitle, errorType: "assign-not-allowed");
            }

            EnsureOpen(ticket);
            kind = targetUserId == access.UserId && ticket.AssignedToUserId is null
                ? TicketChangeKind.Taken
                : TicketChangeKind.Transferred;
            await EnsureAssigneeAsync(ticket, targetUserId, cancellationToken);
        }
        else
        {
            var targetDepartmentId = request.TargetDepartmentId!.Value;
            if (!access.Permissions.CanTransferToDepartment)
            {
                // A department manager in scope moves tickets only between the department's handlers (spec 2.4).
                throw access.Permissions.CanAssign
                    ? new ForbiddenException(TransferNotAllowedTitle, errorType: "transfer-not-allowed")
                    : new ForbiddenException(AssignNotAllowedTitle, errorType: "assign-not-allowed");
            }

            EnsureOpen(ticket);
            kind = TicketChangeKind.Transferred;
            await EnsureTargetDepartmentAsync(ticket, targetDepartmentId, cancellationToken);
        }

        // A reason is required for every transfer; taking a ticket from the queue explains itself.
        if (kind != TicketChangeKind.Taken && string.IsNullOrWhiteSpace(request.Reason))
        {
            throw ValidationException.ForField("reason", HandlingRules.ReasonRequiredMessage);
        }

        var now = clock.UtcNow;
        var change = NewChange(ticket, kind, access.UserId, request.Reason, now);
        if (request.TargetUserId is { } userId)
        {
            ticket.AssignedToUserId = userId;
        }
        else
        {
            // To the department's queue: one of its handlers takes it.
            ticket.AssignedDepartmentId = request.TargetDepartmentId!.Value;
            ticket.AssignedToUserId = null;
        }

        OnHandlerChanged(ticket, change.FromUserId, access.UserId, now, keepStatus: kind == TicketChangeKind.Taken);
        ticket.UpdatedAt = now;
        Complete(change, ticket);

        await SaveAsync(ticket, "assign", access.UserId, cancellationToken);

        logger.LogInformation(
            "Ticket {TicketId} {ChangeKind} by user {UserId}: department {FromDepartmentId} -> {ToDepartmentId}, handler {FromUserId} -> {ToUserId}",
            ticket.Id, kind, access.UserId, change.FromDepartmentId, change.ToDepartmentId, change.FromUserId,
            change.ToUserId);

        return Result(ticket, access);
    }

    // ===================== GET /assign-options =====================

    public async Task<TicketAssignOptionsDto> GetAssignOptionsAsync(int ticketId, CancellationToken cancellationToken)
    {
        var access = await ticketAccess.GetAsync(ticketId, cancellationToken);
        if (!access.Permissions.CanAssign)
        {
            throw new ForbiddenException(AssignNotAllowedTitle, errorType: "assign-not-allowed");
        }

        var ticket = access.Ticket;
        if (ticket.AssignedDepartmentId is not { } handlingDepartmentId)
        {
            return new TicketAssignOptionsDto([], []);
        }

        // The same targets POST /assign accepts (EnsureAssigneeAsync): not the requester, not the current handler.
        var handlers = (await users.GetHandlerOptionsAsync(handlingDepartmentId, cancellationToken))
            .Where(h => h.Id != ticket.RequesterId && h.Id != ticket.AssignedToUserId)
            .ToList();

        IReadOnlyList<NamedRefDto> targetDepartments = [];
        if (access.Permissions.CanTransferToDepartment)
        {
            // EnsureTargetDepartmentAsync: another active department with at least one active handler.
            var others = (await ReferenceDataLookups.DepartmentsAsync(cache, departments, currentUser.OrganizationId,
                    cancellationToken))
                .Where(d => d.IsActive && d.Id != handlingDepartmentId)
                .ToList();
            var withHandlers = (await users.GetHandlerCandidatesAsync(others.Select(d => d.Id).ToList(), null,
                    cancellationToken))
                .Select(h => h.DepartmentId)
                .ToHashSet();
            targetDepartments = others
                .Where(d => withHandlers.Contains(d.Id))
                .OrderBy(d => d.Name, StringComparer.Ordinal)
                .Select(d => new NamedRefDto(d.Id, d.Name))
                .ToList();
        }

        return new TicketAssignOptionsDto(handlers, targetDepartments);
    }

    // ===================== Shared steps =====================

    /// <summary>404 (scope) first, then the tracked row and the version the user saw (409).</summary>
    private async Task<(TicketAccessResult Access, Ticket Ticket)> LoadAsync(int ticketId, byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        var access = await ticketAccess.GetAsync(ticketId, cancellationToken);
        var ticket = await tickets.FindForUpdateAsync(ticketId, cancellationToken)
                     ?? throw new NotFoundException(TicketAccess.TicketNotFoundTitle);

        // Checked now, before the rules run against a state the user has not seen; and again by the UPDATE itself.
        if (!ticket.RowVersion.AsSpan().SequenceEqual(rowVersion))
        {
            logger.LogWarning("Concurrency conflict on ticket {TicketId} by user {UserId} (stale version)",
                ticketId, access.UserId);
            throw Conflict();
        }

        tickets.ExpectRowVersion(ticket, rowVersion);
        return (access, ticket);
    }

    private static void EnsureOpen(Ticket ticket)
    {
        if (ticket.Status is TicketStatus.Resolved or TicketStatus.Closed or TicketStatus.Cancelled)
        {
            throw new BusinessRuleException(NotOpenTitle, errorType: "ticket-not-open");
        }
    }

    /// <summary>An active handler whose department handles the ticket, not its requester and not its current handler.</summary>
    private async Task EnsureAssigneeAsync(Ticket ticket, int targetUserId, CancellationToken cancellationToken)
    {
        if (ticket.AssignedDepartmentId is not { } handlingDepartmentId)
        {
            throw new BusinessRuleException(NotRoutedTitle, errorType: "ticket-not-routed");
        }

        if (targetUserId == ticket.RequesterId)
        {
            throw new BusinessRuleException(AssigneeIsRequesterTitle, errorType: "assignee-is-requester");
        }

        if (targetUserId == ticket.AssignedToUserId)
        {
            throw new BusinessRuleException(AssignmentUnchangedTitle, errorType: "assignment-unchanged");
        }

        // Active, with the Handler role, of this organization (query filter) – from the database, not a cache.
        var candidates = await users.GetHandlerCandidatesAsync([handlingDepartmentId], targetUserId, cancellationToken);
        if (!candidates.Any(c => c.UserId == targetUserId && c.DepartmentId == handlingDepartmentId))
        {
            throw new BusinessRuleException(AssigneeNotHandlerTitle, errorType: "assignee-not-handler");
        }
    }

    /// <summary>Another active department, with at least one active handler – otherwise the ticket would be stuck.</summary>
    private async Task EnsureTargetDepartmentAsync(Ticket ticket, int departmentId, CancellationToken cancellationToken)
    {
        if (departmentId == ticket.AssignedDepartmentId)
        {
            throw new BusinessRuleException(AssignmentUnchangedTitle, errorType: "assignment-unchanged");
        }

        var all = await ReferenceDataLookups.DepartmentsAsync(cache, departments, ticket.OrganizationId,
            cancellationToken);
        if (!all.Any(d => d.Id == departmentId && d.IsActive))
        {
            throw ValidationException.ForField("targetDepartmentId", DepartmentInvalidMessage);
        }

        var handlers = await users.GetHandlerCandidatesAsync([departmentId], null, cancellationToken);
        if (!handlers.Any(h => h.DepartmentId == departmentId))
        {
            throw new BusinessRuleException(NoHandlersTitle, errorType: "department-has-no-handlers");
        }
    }

    private async Task<bool> IsActiveCategoryAsync(int organizationId, int categoryId, CancellationToken cancellationToken)
    {
        var all = await ReferenceDataLookups.CategoriesAsync(cache, categories, organizationId, cancellationToken);
        return all.Any(c => c.Id == categoryId && c.IsActive);
    }

    /// <summary>InProgress → Assigned when the personal handler changed (see the class remarks), with a history row.</summary>
    private static void OnHandlerChanged(Ticket ticket, int? previousHandlerId, int actorUserId, DateTime now,
        bool keepStatus)
    {
        if (keepStatus || ticket.AssignedToUserId == previousHandlerId || ticket.Status != TicketStatus.InProgress)
        {
            return;
        }

        ticket.StatusHistory.Add(new TicketStatusHistory
        {
            PreviousStatus = TicketStatus.InProgress,
            NewStatus = TicketStatus.Assigned,
            ChangedByUserId = actorUserId,
            ChangedAt = now,
        });
        ticket.Status = TicketStatus.Assigned;
    }

    /// <summary>The change row with the "from" side, taken before anything changes.</summary>
    private static TicketChange NewChange(Ticket ticket, TicketChangeKind kind, int actorUserId, string? reason,
        DateTime now)
    {
        var trimmed = reason?.Trim();
        return new TicketChange
        {
            Kind = kind,
            ChangedByUserId = actorUserId,
            ChangedAt = now,
            Reason = string.IsNullOrEmpty(trimmed) ? null : trimmed,
            FromCategoryId = ticket.CategoryId,
            FromUrgency = ticket.Urgency,
            FromDepartmentId = ticket.AssignedDepartmentId,
            FromUserId = ticket.AssignedToUserId,
        };
    }

    /// <summary>The "to" side, after the change; staged with the ticket (one SaveChanges).</summary>
    private static void Complete(TicketChange change, Ticket ticket)
    {
        change.ToCategoryId = ticket.CategoryId;
        change.ToUrgency = ticket.Urgency;
        change.ToDepartmentId = ticket.AssignedDepartmentId;
        change.ToUserId = ticket.AssignedToUserId;
        ticket.Changes.Add(change);
    }

    private async Task SaveAsync(Ticket ticket, string action, int userId, CancellationToken cancellationToken)
    {
        try
        {
            // The ticket, its status history and the change row – one transaction.
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictException exception) when (exception.ErrorType == "concurrency")
        {
            logger.LogWarning("Concurrency conflict on ticket {TicketId} ({Action}) by user {UserId}",
                ticket.Id, action, userId);
            throw Conflict();
        }
    }

    /// <summary>The new version, and whether the user still has the ticket in scope after the change.</summary>
    private static TicketUpdatedDto Result(Ticket ticket, TicketAccessResult access)
    {
        var facts = new TicketAccessFacts(ticket.Id, ticket.RequesterId, ticket.RequesterDepartmentId,
            ticket.AssignedDepartmentId, ticket.AssignedToUserId, ticket.Status);
        return new TicketUpdatedDto(ticket.Id, ticket.RowVersion,
            TicketAccessPolicy.Evaluate(access.Viewer, facts).CanView);
    }

    // Spec 9.5: the ticket's own wording, so the screen can show it as is.
    private static ConflictException Conflict() => new(ConflictTitle, ConflictDetail, "concurrency");
}
