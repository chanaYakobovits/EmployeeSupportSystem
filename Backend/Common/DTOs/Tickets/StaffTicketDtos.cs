using System.Text.Json.Serialization;
using Common.Enums;

namespace Common.DTOs.Tickets;

// What staff with access to a ticket see (handler, department manager, executive – spec 2.4, 7.7). Never returned to
// the ticket's requester, even when the requester is staff too (decision in CLAUDE.md, task 3.2).

/// <summary>A row of the staff lists (spec 7.6).</summary>
/// <param name="Subject">The subject, or – when the ticket has none – the start of the description.</param>
/// <param name="IsSubjectExcerpt">True when <paramref name="Subject"/> is the description excerpt (up to 80 characters).</param>
/// <param name="RequesterDepartmentName">The requester's department when the ticket was opened (a snapshot).</param>
/// <param name="Urgency">Null until set (the fallback path); its SLA is then Medium.</param>
/// <param name="DueAt">Null only for a ticket that was not routed yet.</param>
/// <param name="IsOverdue">Past its due time while the SLA clock runs – computed by the server.</param>
/// <param name="ClassificationSource">For the "classified manually / fallback" mark.</param>
/// <param name="RowVersion">For bulk updates (spec 11.2).</param>
public sealed record StaffTicketListItemDto(
    int Id,
    string Subject,
    bool IsSubjectExcerpt,
    string RequesterName,
    string RequesterDepartmentName,
    string? CategoryName,
    Urgency? Urgency,
    TicketStatus Status,
    string? HandlerName,
    string? AssignedDepartmentName,
    DateTime? DueAt,
    bool IsOverdue,
    ClassificationSource? ClassificationSource,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    byte[] RowVersion);

/// <summary>GET /api/tickets/{id} for staff (spec 7.7). The timeline comes from GET /history; the AI analysis in task 4.4.</summary>
/// <param name="Urgency">Null until set (the fallback path); its SLA is then Medium.</param>
/// <param name="IsOverdue">Past its due time while the SLA clock runs – computed by the server.</param>
/// <param name="RowVersion">Sent back with every update of the ticket (spec 11.2); a newer version → 409.</param>
/// <param name="AllowedActions">
/// What the user may do on this ticket now (<see cref="Constants.TicketActions"/>), by the permission matrix, the status
/// and the assignment – so the screen shows only the allowed actions and never computes permissions itself.
/// </param>
public sealed record StaffTicketDetailsDto(
    int Id,
    string? Subject,
    string Description,
    TicketStatus Status,
    StaffTicketRequesterDto Requester,
    NamedRefDto? Category,
    Urgency? Urgency,
    ClassificationSource? ClassificationSource,
    DateTime? DueAt,
    bool IsOverdue,
    NamedRefDto? AssignedTo,
    NamedRefDto? AssignedDepartment,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ResolvedAt,
    DateTime? ClosedAt,
    byte[] RowVersion,
    IReadOnlyList<string> AllowedActions) : ITicketDetailsDto;

/// <param name="DepartmentName">The department when the ticket was opened (a snapshot).</param>
/// <param name="JobTitle">The role in the organization (spec 7.7).</param>
public sealed record StaffTicketRequesterDto(int Id, string Name, string DepartmentName, string JobTitle);

/// <summary>An id with its display name (category, user, department).</summary>
public sealed record NamedRefDto(int Id, string Name);

/// <summary>GET /api/tickets/{id}/history for staff: the full timeline, internal notes and reasons included.</summary>
public sealed record StaffTicketHistoryDto(int TicketId, IReadOnlyList<StaffTimelineItemDto> Items)
    : ITicketHistoryDto;

/// <summary>One entry of the staff timeline. The JSON has a "kind" discriminator: status / comment / attachment.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(StaffTimelineStatusDto), "status")]
[JsonDerivedType(typeof(StaffTimelineCommentDto), "comment")]
[JsonDerivedType(typeof(StaffTimelineAttachmentDto), "attachment")]
[JsonDerivedType(typeof(StaffTimelineChangeDto), "change")]
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

/// <summary>
/// A reclassification or a transfer, with its reason (task 3.4). Staff only. From/To are names; the ones that did not
/// change are equal. A null department / handler = none (a null handler is the department's queue).
/// </summary>
public sealed record StaffTimelineChangeDto(
    DateTime At,
    TicketChangeKind ChangeKind,
    string ChangedByName,
    string? Reason,
    string? FromCategoryName,
    string? ToCategoryName,
    Urgency? FromUrgency,
    Urgency? ToUrgency,
    string? FromDepartmentName,
    string? ToDepartmentName,
    string? FromHandlerName,
    string? ToHandlerName) : StaffTimelineItemDto(At);
