using System.Linq.Expressions;
using Common.DTOs.Tickets;
using Common.Enums;
using Common.Models;

namespace Services.Implementations.Tickets;

/// <summary>
/// The staff lists (GET /api/tickets?scope=..., spec 7.6, 9.2) as query predicates. The same scopes as
/// <see cref="TicketAccessPolicy"/>, for many tickets at once: a ticket is in a list the user may request exactly when
/// the policy lets the user view it as staff. TicketListScopesTests checks the two against each other – change both together.
/// <list type="bullet">
/// <item>assigned / queue / handling – a handler: assigned to them; their department's queue (no personal handler);
/// every ticket their department handles.</item>
/// <item>department – the executive: the whole organization; a department manager: opened by the department's
/// employees or handled by the department.</item>
/// </list>
/// The user's own tickets are never in a staff list (decision in CLAUDE.md): on them the user always has the requester's
/// view, and they are listed in "הפניות שלי".
/// </summary>
public static class TicketListScopes
{
    public static bool IsAllowed(TicketViewer viewer, TicketListScope scope) => scope switch
    {
        TicketListScope.Assigned or TicketListScope.Queue or TicketListScope.Handling =>
            viewer.Roles.Contains(Role.Handler),
        TicketListScope.Department =>
            viewer.Roles.Contains(Role.Executive)
            || (viewer.Roles.Contains(Role.DepartmentManager) && viewer.ManagedDepartmentId is not null),
        _ => false,
    };

    /// <summary>The tickets of the list. Call only for a scope <see cref="IsAllowed"/> allows.</summary>
    public static Expression<Func<Ticket, bool>> Predicate(TicketViewer viewer, TicketListScope scope)
    {
        if (!IsAllowed(viewer, scope))
        {
            throw new InvalidOperationException($"Scope {scope} is not allowed for user {viewer.UserId}.");
        }

        // Plain values, so the query gets parameters rather than the viewer object.
        var me = viewer.UserId;
        var myDepartment = viewer.DepartmentId;

        switch (scope)
        {
            case TicketListScope.Assigned:
                return t => t.AssignedToUserId == me && t.RequesterId != me;
            case TicketListScope.Queue:
                return t => t.AssignedDepartmentId == myDepartment && t.AssignedToUserId == null && t.RequesterId != me;
            case TicketListScope.Handling:
                return t => t.AssignedDepartmentId == myDepartment && t.RequesterId != me;
            default:
                if (viewer.Roles.Contains(Role.Executive))
                {
                    // The organization query filter limits it to the executive's organization.
                    return t => t.RequesterId != me;
                }

                var managed = viewer.ManagedDepartmentId!.Value;
                return t => (t.RequesterDepartmentId == managed || t.AssignedDepartmentId == managed)
                            && t.RequesterId != me;
        }
    }
}
