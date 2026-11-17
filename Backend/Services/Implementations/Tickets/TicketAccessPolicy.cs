using Common.Constants;
using Common.DTOs.Tickets;
using Common.Enums;

namespace Services.Implementations.Tickets;

/// <summary>What the signed-in user may do with one ticket.</summary>
/// <param name="CanView">Within at least one viewing scope; otherwise the ticket is "not found" (404, spec 2.4).</param>
/// <param name="IsRequesterView">The user opened the ticket: they get the requester's view, even when they are staff too.</param>
/// <param name="CanSeeInternal">Internal notes and their files: staff with a scope on the ticket, never its requester.</param>
/// <param name="CanReply">A comment the requester sees, or a file without a comment: the requester or a handler.</param>
/// <param name="CanWriteInternal">An internal note: a handler or department manager in scope, never the requester.</param>
/// <param name="CanReclassify">
/// Change the category / urgency (spec 2.4): a handler in scope, or the manager of the department that handles the
/// ticket ("בתוך המחלקה") – not of a department whose employee opened a ticket handled elsewhere. Never the requester.
/// </param>
/// <param name="CanAssign">Assign to a handler of the handling department (incl. "קח לטיפולי"): as <paramref name="CanReclassify"/>.</param>
/// <param name="CanTransferToDepartment">Move to another department's queue: a handler in scope only (spec 2.4, task 3.4).</param>
/// <param name="CanChangeStatus">
/// The handler's status transitions (spec 2.4: "שינוי סטטוס" – handlers only; not a department manager or the executive,
/// spec 14 question 8). Which ones, and only for the personal handler, is decided with the status rules. The requester's
/// own transitions go by <paramref name="IsRequesterView"/>.
/// </param>
/// <param name="CanRetryAnalysis">"נתח מחדש" (spec 9.2: מטפל): a handler in scope – not a manager or the executive.</param>
public sealed record TicketPermissions(
    bool CanView,
    bool IsRequesterView,
    bool CanSeeInternal,
    bool CanReply,
    bool CanWriteInternal,
    bool CanReclassify = false,
    bool CanAssign = false,
    bool CanTransferToDepartment = false,
    bool CanChangeStatus = false,
    bool CanRetryAnalysis = false)
{
    public static readonly TicketPermissions None = new(false, false, false, false, false);
}

/// <summary>
/// The permission matrix of spec 2.4 for a single ticket, in one place. Pure – the facts are loaded by
/// <see cref="TicketAccess"/>. Scopes:
/// <list type="bullet">
/// <item>Requester: tickets they opened.</item>
/// <item>Handler: tickets assigned to them, or handled by their department (its queue and every ticket it handles, spec 7.6).</item>
/// <item>Department manager: tickets opened by the department's employees, or handled by the department (spec 4.5).</item>
/// <item>Executive: every ticket of the organization, view only.</item>
/// </list>
/// The staff lists use the same scopes as query predicates (<see cref="TicketListScopes"/>, task 3.4);
/// TicketListScopesTests checks that the two agree – change both together.
/// </summary>
public static class TicketAccessPolicy
{
    public static TicketPermissions Evaluate(TicketViewer viewer, TicketAccessFacts ticket)
    {
        var isRequester = ticket.RequesterId == viewer.UserId;

        var asHandler = viewer.Roles.Contains(Role.Handler)
                        && (ticket.AssignedToUserId == viewer.UserId || ticket.AssignedDepartmentId == viewer.DepartmentId);

        var asManager = viewer.Roles.Contains(Role.DepartmentManager)
                        && viewer.ManagedDepartmentId is int managed
                        && (ticket.RequesterDepartmentId == managed || ticket.AssignedDepartmentId == managed);

        var asExecutive = viewer.Roles.Contains(Role.Executive);

        // A manager acts only on the tickets their department handles (spec 2.4: "בתוך המחלקה").
        var managesHandling = viewer.Roles.Contains(Role.DepartmentManager)
                              && viewer.ManagedDepartmentId is int handled
                              && ticket.AssignedDepartmentId == handled;

        var anyStaffScope = asHandler || asManager || asExecutive;
        if (!isRequester && !anyStaffScope)
        {
            return TicketPermissions.None;
        }

        // The requester's own ticket is always seen as a requester: colleagues' internal notes about it stay hidden.
        var asStaff = !isRequester;

        return new TicketPermissions(
            CanView: true,
            IsRequesterView: isRequester,
            CanSeeInternal: asStaff,
            CanReply: isRequester || asHandler,
            CanWriteInternal: asStaff && (asHandler || asManager),
            CanReclassify: asStaff && (asHandler || managesHandling),
            CanAssign: asStaff && (asHandler || managesHandling),
            CanTransferToDepartment: asStaff && asHandler,
            CanChangeStatus: asStaff && asHandler,
            CanRetryAnalysis: asStaff && asHandler);
    }
}

/// <summary>
/// The actions of <see cref="TicketActions"/> the user may take now: the permission matrix narrowed by the ticket's
/// status and assignment, with the same rules the endpoints enforce (422 ticket-closed / ticket-not-open,
/// ticket-not-routed, the status rules of 3.3, the personal handler and the reopen window). Pure. The screen shows only
/// these – it does not compute permissions itself.
/// </summary>
public static class TicketAllowedActions
{
    public static IReadOnlyList<string> For(TicketPermissions permissions, TicketAccessFacts ticket, TicketViewer viewer,
        DateTime now)
    {
        var actions = new List<string>();
        if (!permissions.CanView)
        {
            return actions;
        }

        // Comments: anything but closed / cancelled (TicketConversationService → 422 ticket-closed).
        var acceptsComments = ticket.Status is not (TicketStatus.Closed or TicketStatus.Cancelled);

        if (permissions.IsRequesterView)
        {
            if (permissions.CanReply && acceptsComments)
            {
                actions.Add(TicketActions.Reply);
            }

            foreach (var transition in TicketStatusRules.From(ticket.Status, StatusActor.Requester))
            {
                if (transition.Action != TicketActions.Reopen || TicketStatusRules.CanReopen(ticket.ResolvedAt, now))
                {
                    actions.Add(transition.Action!);
                }
            }

            return actions;
        }

        // Classification and assignment: not once resolved (TicketHandlingService.EnsureOpen → 422 ticket-not-open).
        var isOpen = ticket.Status is not (TicketStatus.Resolved or TicketStatus.Closed or TicketStatus.Cancelled);
        var isRouted = ticket.AssignedDepartmentId is not null;

        if (permissions.CanReply && acceptsComments)
        {
            actions.Add(TicketActions.Reply);
        }

        if (permissions.CanWriteInternal && acceptsComments)
        {
            actions.Add(TicketActions.InternalNote);
        }

        if (permissions.CanReclassify && isOpen)
        {
            actions.Add(TicketActions.Reclassify);
        }

        if (permissions.CanAssign && isOpen && isRouted)
        {
            actions.Add(TicketActions.Assign);
        }

        if (permissions.CanTransferToDepartment && isOpen && isRouted)
        {
            actions.Add(TicketActions.TransferToDepartment);
        }

        // In the queue of the user's own department: an active handler there may take it (EnsureAssigneeAsync).
        var inMyQueue = ticket.AssignedToUserId is null && viewer.Roles.Contains(Role.Handler)
                        && ticket.AssignedDepartmentId is not null && ticket.AssignedDepartmentId == viewer.DepartmentId;
        if (permissions.CanAssign && isOpen && inMyQueue)
        {
            actions.Add(TicketActions.TakeFromQueue);
        }

        // The AI analysis (task 4.4): seen by every staff scope; re-analysis and applying it by the rules above.
        if (permissions.CanSeeInternal)
        {
            actions.Add(TicketActions.ViewAiAnalysis);
        }

        if (ticket.Ai is { } ai)
        {
            if (permissions.CanRetryAnalysis && isOpen && !ai.AnalysisInProgress)
            {
                actions.Add(TicketActions.RetryAnalysis);
            }

            if (permissions.CanReclassify && isOpen && ai.RecommendationDiffers)
            {
                actions.Add(TicketActions.ApplyRecommendation);
            }
        }

        if (permissions.CanChangeStatus)
        {
            foreach (var transition in TicketStatusRules.From(ticket.Status, StatusActor.Handler))
            {
                // The personal handler only (TicketStatusService); "התחל טיפול" also from the queue (and assigns).
                if (ticket.AssignedToUserId == viewer.UserId
                    || (transition.Action == TicketActions.Start && inMyQueue))
                {
                    actions.Add(transition.Action!);
                }
            }
        }

        return actions;
    }
}
