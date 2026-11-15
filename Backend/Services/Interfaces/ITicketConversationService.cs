using Common.DTOs.Tickets;

namespace Services.Interfaces;

/// <summary>
/// The conversation inside a ticket (task 3.2): comments, internal notes, attachments and the timeline. Every method
/// checks the ticket with <see cref="ITicketAccess"/> first: outside the user's scope → 404.
/// </summary>
public interface ITicketConversationService
{
    /// <summary>Adds a comment (or an internal note) with up to 3 files.</summary>
    /// <exception cref="Common.Exceptions.ValidationException">Invalid body or files.</exception>
    /// <exception cref="Common.Exceptions.NotFoundException">Outside the user's scope.</exception>
    /// <exception cref="Common.Exceptions.ForbiddenException">An internal note without the right to write one, or a
    /// comment by a user who may only view (department manager, executive).</exception>
    /// <exception cref="Common.Exceptions.BusinessRuleException">The ticket is closed or cancelled.</exception>
    /// <exception cref="Common.Exceptions.ConflictException">The ticket changed while a visible comment was added.</exception>
    Task<CommentCreatedDto> AddCommentAsync(int ticketId, CreateCommentRequest request, IReadOnlyList<IncomingFile> files,
        CancellationToken cancellationToken);

    /// <summary>Attaches 1–3 files without a comment (visible to the requester). Same rules as a visible comment.</summary>
    Task<IReadOnlyList<TicketAttachmentDto>> AddAttachmentsAsync(int ticketId, IReadOnlyList<IncomingFile> files,
        CancellationToken cancellationToken);

    /// <summary>
    /// Opens an attachment for download and records the download in the audit log (spec 6.4). A file of an internal note
    /// is "not found" for whoever may not see internal notes.
    /// </summary>
    /// <param name="ipAddress">The client address, for the audit log.</param>
    Task<AttachmentDownload> DownloadAttachmentAsync(int ticketId, int attachmentId, string ipAddress,
        CancellationToken cancellationToken);

    /// <summary>
    /// The timeline: <see cref="RequesterTicketHistoryDto"/> for the requester (without internal notes, their files and
    /// reasons), <see cref="StaffTicketHistoryDto"/> for staff with a scope.
    /// </summary>
    Task<ITicketHistoryDto> GetHistoryAsync(int ticketId, CancellationToken cancellationToken);
}
