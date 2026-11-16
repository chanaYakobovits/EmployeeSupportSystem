using System.Text.Json.Serialization;
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
/// <param name="RowVersion">The version for the requester's status changes (cancel, confirm, reopen – task 3.5).</param>
/// <param name="AllowedActions">
/// What the requester may do now (values of <see cref="Constants.TicketActions"/>: reply, cancel, confirmResolved,
/// reopen), computed by the server from the status rules.
/// </param>
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
    IReadOnlyList<RequesterTicketHistoryItemDto> Timeline,
    byte[] RowVersion,
    IReadOnlyList<string> AllowedActions) : ITicketDetailsDto;

/// <summary>
/// GET /api/tickets/{id}: <see cref="RequesterTicketDetailsDto"/> for the ticket's requester (even when they are staff
/// too), <see cref="StaffTicketDetailsDto"/> for staff in scope.
/// </summary>
public interface ITicketDetailsDto
{
    int Id { get; }
}

/// <summary>A status change in the requester's timeline. The reason is not included (it may be internal).</summary>
/// <param name="PreviousStatus">Null for the opening entry.</param>
/// <param name="ChangedByName">Null when the system made the change.</param>
public sealed record RequesterTicketHistoryItemDto(
    TicketStatus? PreviousStatus,
    TicketStatus NewStatus,
    DateTime ChangedAt,
    string? ChangedByName);

// ===================== Conversation and timeline (task 3.2) =====================

/// <summary>
/// An attached file as listed in a timeline (requester and staff alike: no internal field). The original name comes from
/// the database, sanitized on upload; the content is downloaded through GET /api/tickets/{id}/attachments/{attId}.
/// </summary>
/// <param name="ContentType">The type detected from the content on upload, not the one the client sent.</param>
public sealed record TicketAttachmentDto(
    int Id,
    string FileName,
    string ContentType,
    int SizeBytes,
    string UploadedByName,
    DateTime CreatedAt);

/// <summary>
/// GET /api/tickets/{id}/history for the requester: status changes, comments and attachments in order. Internal notes,
/// their files and status-change reasons are left out by the queries themselves, not by the client.
/// </summary>
public sealed record RequesterTicketHistoryDto(int TicketId, IReadOnlyList<RequesterTimelineItemDto> Items)
    : ITicketHistoryDto;

/// <summary>GET /api/tickets/{id}/history: <see cref="RequesterTicketHistoryDto"/> or <see cref="StaffTicketHistoryDto"/>.</summary>
public interface ITicketHistoryDto
{
    int TicketId { get; }
}

/// <summary>One entry of the requester's timeline. The JSON has a "kind" discriminator: status / comment / attachment.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(RequesterTimelineStatusDto), "status")]
[JsonDerivedType(typeof(RequesterTimelineCommentDto), "comment")]
[JsonDerivedType(typeof(RequesterTimelineAttachmentDto), "attachment")]
public abstract record RequesterTimelineItemDto(DateTime At);

/// <summary>A status change. The reason is not included (it may be internal).</summary>
/// <param name="ChangedByName">Null when the system made the change.</param>
public sealed record RequesterTimelineStatusDto(
    DateTime At,
    TicketStatus? PreviousStatus,
    TicketStatus NewStatus,
    string? ChangedByName) : RequesterTimelineItemDto(At);

/// <summary>A comment visible to the requester, with the files attached to it.</summary>
/// <param name="IsFromRequester">Written by the requester (shown on the requester's side of the conversation).</param>
/// <param name="IsResolution">The handler's resolution reply (task 3.5).</param>
public sealed record RequesterTimelineCommentDto(
    DateTime At,
    int Id,
    string AuthorName,
    bool IsFromRequester,
    string Body,
    bool IsResolution,
    IReadOnlyList<TicketAttachmentDto> Attachments) : RequesterTimelineItemDto(At);

/// <summary>A file attached without a comment (including the files of the new-ticket form).</summary>
public sealed record RequesterTimelineAttachmentDto(DateTime At, TicketAttachmentDto Attachment)
    : RequesterTimelineItemDto(At);
