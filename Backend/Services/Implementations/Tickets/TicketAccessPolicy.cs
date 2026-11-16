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
public sealed record TicketPermissions(
    bool CanView,
    bool IsRequesterView,
    bool CanSeeInternal,
    bool CanReply,
    bool CanWriteInternal,
    bool CanReclassify = false,
    bool CanAssign = false,
    bool CanTransferToDepartment = false)
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
            CanTransferToDepartment: asStaff && asHandler);
    }
}

/// <summary>
/// The actions of <see cref="TicketActions"/> the user may take now (task 3.4): the permission matrix narrowed by the
/// ticket's status and assignment, with the same rules the endpoints enforce (422 ticket-closed / ticket-not-open,
/// ticket-not-routed). Pure. The screen shows only these – it does not compute permissions itself.
/// </summary>
public static class TicketAllowedActions
{
    public static IReadOnlyList<string> For(TicketPermissions permissions, TicketAccessFacts ticket, TicketViewer viewer)
    {
        var actions = new List<string>();
        if (!permissions.CanView || permissions.IsRequesterView)
        {
            return actions;
        }

        // Comments: anything but closed / cancelled (TicketConversationService → 422 ticket-closed).
        var acceptsComments = ticket.Status is not (TicketStatus.Closed or TicketStatus.Cancelled);
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
        if (permissions.CanAssign && isOpen && ticket.AssignedToUserId is null
            && viewer.Roles.Contains(Role.Handler) && ticket.AssignedDepartmentId == viewer.DepartmentId)
        {
            actions.Add(TicketActions.TakeFromQueue);
        }

        return actions;
    }
}
