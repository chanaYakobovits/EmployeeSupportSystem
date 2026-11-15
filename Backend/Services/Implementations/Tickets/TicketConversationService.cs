using Common.Constants;
using Common.DTOs.Tickets;
using Common.Enums;
using Common.Exceptions;
using Common.Interfaces;
using Common.Models;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Repositories.Interfaces;
using Services.Implementations.Files;
using Services.Interfaces;
using Services.Mapping;
using Services.Validators;

namespace Services.Implementations.Tickets;

/// <summary>
/// Comments, internal notes and attachments of a ticket (task 3.2). Order of checks: input (400) → scope (404) →
/// permission (403) → ticket state (422). Logs ids, sizes and types only – never a comment body or a file name (spec 6.4).
/// </summary>
public sealed class TicketConversationService(
    ITicketAccess ticketAccess,
    ITicketRepository tickets,
    ITicketConversationRepository conversation,
    IAuditLogRepository auditLog,
    IUnitOfWork unitOfWork,
    IFileStorage storage,
    IDateTimeProvider clock,
    ICurrentUser currentUser,
    IValidator<CreateCommentRequest> commentValidator,
    IValidator<IReadOnlyList<IncomingFile>> filesValidator,
    ILogger<TicketConversationService> logger) : ITicketConversationService
{
    public const string AttachmentNotFoundTitle = "הקובץ לא נמצא";
    public const string TicketClosedTitle = "לא ניתן להוסיף תגובות או קבצים לפנייה שנסגרה או בוטלה";
    public const string InternalNotAllowedTitle = "אין לך הרשאה לכתוב הערה פנימית בפנייה זו";
    public const string ReplyNotAllowedTitle = "אין לך הרשאה להגיב או לצרף קבצים בפנייה זו";
    public const string NoFilesMessage = "יש לבחור קובץ לצירוף";

    /// <summary>No comment or file is added once the ticket is final (spec 3.2: "סופית. אין שינויים נוספים").</summary>
    private static readonly TicketStatus[] FinalStatuses = [TicketStatus.Closed, TicketStatus.Cancelled];

    public async Task<CommentCreatedDto> AddCommentAsync(int ticketId, CreateCommentRequest request,
        IReadOnlyList<IncomingFile> files, CancellationToken cancellationToken)
    {
        await commentValidator.EnsureValidAsync(request, cancellationToken);
        await filesValidator.EnsureValidAsync(files, cancellationToken);

        var access = await ticketAccess.GetAsync(ticketId, cancellationToken);
        if (request.IsInternal && !access.Permissions.CanWriteInternal)
        {
            throw new ForbiddenException(InternalNotAllowedTitle, errorType: "internal-comment-not-allowed");
        }

        if (!request.IsInternal && !access.Permissions.CanReply)
        {
            throw new ForbiddenException(ReplyNotAllowedTitle, errorType: "reply-not-allowed");
        }

        EnsureNotFinal(access.Ticket.Status);

        var now = clock.UtcNow;
        if (!request.IsInternal)
        {
            // A visible comment is an update the requester sees ("עדכון אחרון"). An internal note is not: touching the
            // ticket would show the requester an update with nothing visible in it.
            await TouchAsync(ticketId, now, cancellationToken);
        }

        var comment = request.ToNewComment(ticketId, access.UserId, now);

        await using var staged = new StagedFiles(storage, logger);
        foreach (var file in files)
        {
            var attachment = await staged.SaveAttachmentAsync(file, access.UserId, now, cancellationToken);
            // A file of an internal note is internal with it: it is reached only through the comment.
            attachment.TicketId = ticketId;
            comment.Attachments.Add(attachment);
        }

        conversation.AddComment(comment);

        // Task 3.5: a requester reply while WaitingForEmployee moves the ticket to InProgress (TicketStatusRules),
        // in this same save, with a history entry.

        await unitOfWork.SaveChangesAsync(cancellationToken);
        staged.Commit();

        logger.LogInformation(
            "Comment {CommentId} added to ticket {TicketId} by user {UserId} (internal: {IsInternal}, attachments: {AttachmentCount})",
            comment.Id, ticketId, access.UserId, comment.IsInternal, comment.Attachments.Count);
        LogAttachments(comment.Attachments, ticketId, access.UserId);

        return new CommentCreatedDto(comment.Id);
    }

    public async Task<IReadOnlyList<TicketAttachmentDto>> AddAttachmentsAsync(int ticketId,
        IReadOnlyList<IncomingFile> files, CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            throw Common.Exceptions.ValidationException.ForField(AttachmentFilesValidator.FieldName, NoFilesMessage);
        }

        await filesValidator.EnsureValidAsync(files, cancellationToken);

        var access = await ticketAccess.GetAsync(ticketId, cancellationToken);
        if (!access.Permissions.CanReply)
        {
            throw new ForbiddenException(ReplyNotAllowedTitle, errorType: "reply-not-allowed");
        }

        EnsureNotFinal(access.Ticket.Status);

        var now = clock.UtcNow;
        await TouchAsync(ticketId, now, cancellationToken);

        var attachments = new List<TicketAttachment>(files.Count);
        await using var staged = new StagedFiles(storage, logger);
        foreach (var file in files)
        {
            var attachment = await staged.SaveAttachmentAsync(file, access.UserId, now, cancellationToken);
            attachment.TicketId = ticketId;
            conversation.AddAttachment(attachment);
            attachments.Add(attachment);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        staged.Commit();

        LogAttachments(attachments, ticketId, access.UserId);
        return await conversation.GetAttachmentDtosAsync(ticketId, attachments.Select(a => a.Id).ToList(),
            cancellationToken);
    }

    public async Task<AttachmentDownload> DownloadAttachmentAsync(int ticketId, int attachmentId, string ipAddress,
        CancellationToken cancellationToken)
    {
        var access = await ticketAccess.GetAsync(ticketId, cancellationToken);

        // The file of an internal note is filtered in the query – for the requester it is exactly like a missing one.
        var info = await conversation.GetAttachmentAsync(ticketId, attachmentId, access.Permissions.CanSeeInternal,
                       cancellationToken)
                   ?? throw new NotFoundException(AttachmentNotFoundTitle);

        Stream content;
        try
        {
            content = await storage.OpenReadAsync(info.StoredFileName, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            logger.LogWarning("Attachment {AttachmentId} of ticket {TicketId} has no stored file", attachmentId, ticketId);
            throw new NotFoundException(AttachmentNotFoundTitle);
        }

        try
        {
            // Spec 6.4: a file download is a sensitive action. Recorded once the file is known to be there.
            auditLog.Add(AuditEntries.ForEntity(AuditActions.AttachmentDownloaded, AuditEntityTypes.TicketAttachment,
                attachmentId, currentUser.OrganizationId, access.UserId, ipAddress, clock.UtcNow));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await content.DisposeAsync();
            throw;
        }

        var contentType = AttachmentFileTypes.SafeContentType(info.ContentType);
        logger.LogInformation(
            "Attachment {AttachmentId} of ticket {TicketId} downloaded by user {UserId} ({SizeBytes} bytes, {ContentType})",
            attachmentId, ticketId, access.UserId, info.SizeBytes, contentType);

        return new AttachmentDownload(content, info.OriginalFileName, contentType, info.SizeBytes);
    }

    public async Task<ITicketHistoryDto> GetHistoryAsync(int ticketId, CancellationToken cancellationToken)
    {
        var access = await ticketAccess.GetAsync(ticketId, cancellationToken);

        // Two separate queries and DTO types: what the requester may not see is never selected (spec 6.3).
        return access.Permissions.CanSeeInternal
            ? new StaffTicketHistoryDto(ticketId, await conversation.GetStaffTimelineAsync(ticketId, cancellationToken))
            : new RequesterTicketHistoryDto(ticketId,
                await conversation.GetRequesterTimelineAsync(ticketId, cancellationToken));
    }

    /// <summary>
    /// Sets UpdatedAt on the tracked ticket (saved with the rest). Its RowVersion changes, so a handler who still has the
    /// old version gets 409 on the next update and sees the new comment; a status change in between → 409 here.
    /// The state is checked again on the tracked row, the one the save is conditioned on.
    /// </summary>
    private async Task TouchAsync(int ticketId, DateTime now, CancellationToken cancellationToken)
    {
        var ticket = await tickets.FindForUpdateAsync(ticketId, cancellationToken)
                     ?? throw new NotFoundException(TicketAccess.TicketNotFoundTitle);
        EnsureNotFinal(ticket.Status);
        ticket.UpdatedAt = now;
    }

    private static void EnsureNotFinal(TicketStatus status)
    {
        if (FinalStatuses.Contains(status))
        {
            throw new BusinessRuleException(TicketClosedTitle, errorType: "ticket-closed");
        }
    }

    private void LogAttachments(IEnumerable<TicketAttachment> attachments, int ticketId, int userId)
    {
        foreach (var attachment in attachments)
        {
            logger.LogInformation(
                "Attachment {AttachmentId} added to ticket {TicketId} by user {UserId} ({SizeBytes} bytes, {ContentType})",
                attachment.Id, ticketId, userId, attachment.SizeBytes, attachment.ContentType);
        }
    }
}
