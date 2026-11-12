using Common.Enums;

namespace Common.DTOs.Tickets;

// What the requester (the employee who opened the ticket) sees – spec 7.3, 7.5.
// Never add: Urgency, DueAt, AiStatus, the AI analysis, ClassificationSource, history reasons or internal comments
// (spec 2.4, 6.3). RequesterJsonTests fails if one of them appears.

/// <summary>Response of POST /api/tickets: the ticket number ("פנייה מס' 1042").</summary>
public sealed record TicketCreatedDto(int Id);

/// <summary>A row of "הפניות שלי".</summary>
/// <param name="Subject">The subject, or – when the ticket has none – the start of the description.</param>
/// <param name="IsSubjectExcerpt">True when <paramref name="Subject"/> is the description excerpt (up to 80 characters).</param>
/// <param name="CategoryName">Null until the ticket is classified (the client shows "מתבצע שיוך...").</param>
/// <param name="HandlerName">Null while no personal handler is assigned.</param>
public sealed record RequesterTicketListItemDto(
    int Id,
    string Subject,
    bool IsSubjectExcerpt,
    string? CategoryName,
    TicketStatus Status,
    string? HandlerName,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>The summary cards of "הפניות שלי" (spec 7.3).</summary>
/// <param name="Open">New, Assigned, InProgress and Resolved (not yet confirmed).</param>
/// <param name="WaitingForMe">WaitingForEmployee.</param>
/// <param name="ClosedThisMonth">Closed since the start of the current month, Israel time.</param>
public sealed record RequesterTicketsSummaryDto(int Open, int WaitingForMe, int ClosedThisMonth);

/// <summary>GET /api/tickets/{id} for the requester.</summary>
public sealed record RequesterTicketDetailsDto(
    int Id,
    string? Subject,
    string Description,
    TicketStatus Status,
    string? CategoryName,
    string? HandlerName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ResolvedAt,
    DateTime? ClosedAt,
    IReadOnlyList<RequesterTicketHistoryItemDto> Timeline);

/// <summary>A status change in the requester's timeline. The reason is not included (it may be internal).</summary>
/// <param name="PreviousStatus">Null for the opening entry.</param>
/// <param name="ChangedByName">Null when the system made the change.</param>
public sealed record RequesterTicketHistoryItemDto(
    TicketStatus? PreviousStatus,
    TicketStatus NewStatus,
    DateTime ChangedAt,
    string? ChangedByName);
