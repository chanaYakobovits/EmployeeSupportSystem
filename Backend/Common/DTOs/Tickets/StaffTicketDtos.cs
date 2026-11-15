using System.Text.Json.Serialization;
using Common.Enums;

namespace Common.DTOs.Tickets;

// What staff with access to a ticket see (handler, department manager, executive – spec 2.4, 7.7). Never returned to
// the ticket's requester, even when the requester is staff too (decision in CLAUDE.md, task 3.2).

/// <summary>GET /api/tickets/{id}/history for staff: the full timeline, internal notes and reasons included.</summary>
public sealed record StaffTicketHistoryDto(int TicketId, IReadOnlyList<StaffTimelineItemDto> Items)
    : ITicketHistoryDto;

/// <summary>One entry of the staff timeline. The JSON has a "kind" discriminator: status / comment / attachment.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(StaffTimelineStatusDto), "status")]
[JsonDerivedType(typeof(StaffTimelineCommentDto), "comment")]
[JsonDerivedType(typeof(StaffTimelineAttachmentDto), "attachment")]
public abstract record StaffTimelineItemDto(DateTime At);

/// <param name="ChangedByName">Null when the system made the change.</param>
/// <param name="Reason">The reason given for the change (e.g. reclassification), if any.</param>
public sealed record StaffTimelineStatusDto(
    DateTime At,
    TicketStatus? PreviousStatus,
    TicketStatus NewStatus,
    string? ChangedByName,
    string? Reason) : StaffTimelineItemDto(At);

/// <param name="IsInternal">An internal note: never shown to the requester, nor its files.</param>
public sealed record StaffTimelineCommentDto(
    DateTime At,
    int Id,
    string AuthorName,
    bool IsFromRequester,
    string Body,
    bool IsInternal,
    bool IsResolution,
    IReadOnlyList<TicketAttachmentDto> Attachments) : StaffTimelineItemDto(At);

public sealed record StaffTimelineAttachmentDto(DateTime At, TicketAttachmentDto Attachment) : StaffTimelineItemDto(At);
