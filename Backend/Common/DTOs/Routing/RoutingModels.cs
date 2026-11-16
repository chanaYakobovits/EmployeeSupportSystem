using Common.Enums;

namespace Common.DTOs.Routing;

/// <summary>
/// An active user with the Handler role who may receive a ticket, with their current load: assigned tickets in
/// <see cref="Constants.TicketStatuses.Unresolved"/> (New, Assigned, InProgress, WaitingForEmployee).
/// </summary>
public sealed record HandlerCandidate(int UserId, int DepartmentId, int OpenTicketCount);

/// <summary>Why a ticket went where it went (logged with the ids – never with content).</summary>
public enum RoutingReason
{
    /// <summary>FixedDepartment: a handler (or the queue) of the category's handling department.</summary>
    FixedDepartment,

    /// <summary>RequesterDepartmentHr: the departmental HR handler of the requester's department.</summary>
    DepartmentHr,

    /// <summary>RequesterDepartmentHr, but the requester's department has no HR handler → central HR.</summary>
    DepartmentHrMissing,

    /// <summary>
    /// RequesterDepartmentHr, but the HR handler is not available (inactive, no Handler role, or is the requester)
    /// → central HR.
    /// </summary>
    DepartmentHrUnavailable,

    /// <summary>FixedDepartment, but the handling department has no available handler → central HR.</summary>
    NoHandlerInDepartment,

    /// <summary>
    /// Reclassified to a category of the department that already handles the ticket: its handler keeps it (task 3.4).
    /// </summary>
    SameDepartmentKeptHandler
}

/// <summary>
/// Where a ticket goes: the handling department and, if one is available, the handler. A null handler = the
/// department's queue. <see cref="Reason"/> says which rule decided.
/// </summary>
public sealed record RoutingDecision(int DepartmentId, int? HandlerUserId, RoutingReason Reason);

/// <summary>
/// A classification to apply to a ticket – by the fallback (task 3.3), the AI (4.3) or a handler (3.4).
/// A null urgency is allowed (the fallback): the SLA then uses Medium (spec 3.4).
/// </summary>
public sealed record TicketClassification(int CategoryId, Urgency? Urgency, ClassificationSource Source);
