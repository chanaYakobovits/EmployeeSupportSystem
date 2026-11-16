using Common.Constants;
using Common.Enums;

namespace Services.Implementations.Tickets;

/// <summary>One allowed transition of spec 3.3.</summary>
/// <param name="Action">The <see cref="TicketActions"/> value of a user's transition; null for the system's.</param>
public sealed record StatusTransition(TicketStatus From, TicketStatus To, StatusActor Actor, string? Action);

/// <summary>
/// The status transitions of spec 3.3 – from which status, to which, and who – in one place, used by the API
/// (PATCH /status, allowedActions), every automatic transition (routing, the employee's reply, a handler change, the
/// auto-close job) and the tests. Any other transition is rejected (422). Pure.
/// </summary>
public static class TicketStatusRules
{
    /// <summary>
    /// After a resolution, the employee may confirm or reopen for this long; then the ticket closes automatically
    /// (spec 3.3, 14.9). One value for both, so that a ticket is never both reopenable and due for closing.
    /// </summary>
    public static readonly TimeSpan ResolvedWindow = TimeSpan.FromDays(7);

    /// <summary>
    /// The personal handler is gone (moved to someone else, or deactivated): an InProgress ticket is back to Assigned –
    /// nobody has started it (decision in CLAUDE.md). A system transition, recorded with whoever caused it.
    /// </summary>
    public static readonly StatusTransition HandlerRemoved =
        new(TicketStatus.InProgress, TicketStatus.Assigned, StatusActor.System, null);

    public static readonly IReadOnlyList<StatusTransition> Transitions =
    [
        // Routing after the AI analysis or the fallback (TicketClassifier).
        new(TicketStatus.New, TicketStatus.Assigned, StatusActor.System, null),
        new(TicketStatus.New, TicketStatus.Cancelled, StatusActor.Requester, TicketActions.Cancel),
        new(TicketStatus.Assigned, TicketStatus.Cancelled, StatusActor.Requester, TicketActions.Cancel),
        // Without a personal handler, the handler who starts is assigned (TicketStatusService).
        new(TicketStatus.Assigned, TicketStatus.InProgress, StatusActor.Handler, TicketActions.Start),
        new(TicketStatus.InProgress, TicketStatus.WaitingForEmployee, StatusActor.Handler, TicketActions.RequestInfo),
        // Requires the resolution reply to the employee.
        new(TicketStatus.InProgress, TicketStatus.Resolved, StatusActor.Handler, TicketActions.Resolve),
        // The employee replied (TicketConversationService).
        new(TicketStatus.WaitingForEmployee, TicketStatus.InProgress, StatusActor.System, null),
        new(TicketStatus.Resolved, TicketStatus.Closed, StatusActor.Requester, TicketActions.ConfirmResolved),
        // 7 days after the resolution (AutoCloseService).
        new(TicketStatus.Resolved, TicketStatus.Closed, StatusActor.System, null),
        // Within ResolvedWindow (checked with CanReopen).
        new(TicketStatus.Resolved, TicketStatus.InProgress, StatusActor.Requester, TicketActions.Reopen),
        HandlerRemoved,
    ];

    /// <summary>The Hebrew names of spec 3.2, for the 422 messages.</summary>
    public static string Label(TicketStatus status) => status switch
    {
        TicketStatus.New => "חדשה",
        TicketStatus.Assigned => "שויכה",
        TicketStatus.InProgress => "בטיפול",
        TicketStatus.WaitingForEmployee => "ממתינה לעובד",
        TicketStatus.Resolved => "נפתרה",
        TicketStatus.Closed => "סגורה",
        TicketStatus.Cancelled => "בוטלה",
        _ => status.ToString(),
    };

    public static StatusTransition? Find(TicketStatus from, TicketStatus to, StatusActor actor) =>
        Transitions.FirstOrDefault(t => t.From == from && t.To == to && t.Actor == actor);

    public static bool IsAllowed(TicketStatus from, TicketStatus to, StatusActor actor) => Find(from, to, actor) is not null;

    /// <summary>The transitions <paramref name="actor"/> may make from <paramref name="from"/>.</summary>
    public static IEnumerable<StatusTransition> From(TicketStatus from, StatusActor actor) =>
        Transitions.Where(t => t.From == from && t.Actor == actor);

    /// <summary>"עד 7 ימים מהפתרון": strictly before the moment the ticket is due for closing.</summary>
    public static bool CanReopen(DateTime? resolvedAt, DateTime now) =>
        resolvedAt is { } resolved && now < resolved + ResolvedWindow;

    /// <summary>7 days after the resolution, exactly – not a moment before.</summary>
    public static bool IsAutoCloseDue(DateTime? resolvedAt, DateTime now) =>
        resolvedAt is { } resolved && now >= resolved + ResolvedWindow;

    /// <summary>The latest resolution time that is due for closing at <paramref name="now"/>.</summary>
    public static DateTime AutoCloseCutoff(DateTime now) => now - ResolvedWindow;
}
