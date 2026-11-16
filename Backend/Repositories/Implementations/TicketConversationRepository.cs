using Common.DTOs.Tickets;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Repositories.Data;
using Repositories.Interfaces;

namespace Repositories.Implementations;

public sealed class TicketConversationRepository(AppDbContext db) : ITicketConversationRepository
{
    // Order of entries with the same time: the status change, then comments, then files without a comment.
    private const int StatusOrder = 0;
    private const int CommentOrder = 1;
    private const int AttachmentOrder = 2;
    private const int ChangeOrder = 3;

    public void AddComment(TicketComment comment) => db.TicketComments.Add(comment);

    public void AddAttachment(TicketAttachment attachment) => db.TicketAttachments.Add(attachment);

    public Task<StoredAttachmentInfo?> GetAttachmentAsync(int ticketId, int attachmentId, bool includeInternal,
        CancellationToken cancellationToken) =>
        Ticket(ticketId)
            .SelectMany(t => t.Attachments)
            .Where(a => a.Id == attachmentId)
            // The file of an internal note is internal too (spec 6.3) – filtered here, in the query.
            .Where(a => includeInternal || a.CommentId == null || !a.Comment!.IsInternal)
            .Select(a => new StoredAttachmentInfo(a.Id, a.StoredFileName, a.OriginalFileName, a.ContentType, a.SizeBytes))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TicketAttachmentDto>> GetAttachmentDtosAsync(int ticketId,
        IReadOnlyCollection<int> attachmentIds, CancellationToken cancellationToken) =>
        await Ticket(ticketId)
            .SelectMany(t => t.Attachments)
            .Where(a => attachmentIds.Contains(a.Id))
            .OrderBy(a => a.Id)
            .Select(a => new TicketAttachmentDto(a.Id, a.OriginalFileName, a.ContentType, a.SizeBytes,
                a.UploadedByUser.FirstName + " " + a.UploadedByUser.LastName, a.CreatedAt))
            .ToListAsync(cancellationToken);

    // ===================== Timeline =====================

    public async Task<IReadOnlyList<RequesterTimelineItemDto>> GetRequesterTimelineAsync(int ticketId,
        CancellationToken cancellationToken)
    {
        // Index TicketStatusHistory(TicketId, ChangedAt). The reason is deliberately not selected (it may be internal).
        var statuses = await Ticket(ticketId)
            .SelectMany(t => t.StatusHistory)
            .Select(h => new
            {
                h.Id,
                Item = new RequesterTimelineStatusDto(h.ChangedAt, h.PreviousStatus, h.NewStatus,
                    h.ChangedByUser != null ? h.ChangedByUser.FirstName + " " + h.ChangedByUser.LastName : null),
            })
            .ToListAsync(cancellationToken);

        // Index TicketComments(TicketId, CreatedAt). Internal notes – and with them their files – are not selected.
        var comments = await Ticket(ticketId)
            .SelectMany(t => t.Comments)
            .Where(c => !c.IsInternal)
            .Select(c => new
            {
                c.Id,
                Item = new RequesterTimelineCommentDto(c.CreatedAt, c.Id,
                    c.AuthorUser.FirstName + " " + c.AuthorUser.LastName,
                    c.AuthorUserId == c.Ticket.RequesterId,
                    c.Body,
                    c.IsResolution,
                    c.Attachments
                        .OrderBy(a => a.Id)
                        .Select(a => new TicketAttachmentDto(a.Id, a.OriginalFileName, a.ContentType, a.SizeBytes,
                            a.UploadedByUser.FirstName + " " + a.UploadedByUser.LastName, a.CreatedAt))
                        .ToList()),
            })
            .ToListAsync(cancellationToken);

        var files = await AttachmentsWithoutComment(ticketId).ToListAsync(cancellationToken);

        return Merge(
            statuses.Select(s => (s.Item.At, StatusOrder, s.Id, (RequesterTimelineItemDto)s.Item)),
            comments.Select(c => (c.Item.At, CommentOrder, c.Id, (RequesterTimelineItemDto)c.Item)),
            files.Select(f => (f.CreatedAt, AttachmentOrder, f.Id,
                (RequesterTimelineItemDto)new RequesterTimelineAttachmentDto(f.CreatedAt, f))));
    }

    public async Task<IReadOnlyList<StaffTimelineItemDto>> GetStaffTimelineAsync(int ticketId,
        CancellationToken cancellationToken)
    {
        var statuses = await Ticket(ticketId)
            .SelectMany(t => t.StatusHistory)
            .Select(h => new
            {
                h.Id,
                Item = new StaffTimelineStatusDto(h.ChangedAt, h.PreviousStatus, h.NewStatus,
                    h.ChangedByUser != null ? h.ChangedByUser.FirstName + " " + h.ChangedByUser.LastName : null,
                    h.Reason),
            })
            .ToListAsync(cancellationToken);

        var comments = await Ticket(ticketId)
            .SelectMany(t => t.Comments)
            .Select(c => new
            {
                c.Id,
                Item = new StaffTimelineCommentDto(c.CreatedAt, c.Id,
                    c.AuthorUser.FirstName + " " + c.AuthorUser.LastName,
                    c.AuthorUserId == c.Ticket.RequesterId,
                    c.Body,
                    c.IsInternal,
                    c.IsResolution,
                    c.Attachments
                        .OrderBy(a => a.Id)
                        .Select(a => new TicketAttachmentDto(a.Id, a.OriginalFileName, a.ContentType, a.SizeBytes,
                            a.UploadedByUser.FirstName + " " + a.UploadedByUser.LastName, a.CreatedAt))
                        .ToList()),
            })
            .ToListAsync(cancellationToken);

        var files = await AttachmentsWithoutComment(ticketId).ToListAsync(cancellationToken);

        // Reclassifications and transfers with their reasons (task 3.4) – a table no requester query reads.
        var changes = await Ticket(ticketId)
            .SelectMany(t => t.Changes)
            .Select(c => new
            {
                c.Id,
                Item = new StaffTimelineChangeDto(c.ChangedAt, c.Kind,
                    c.ChangedByUser.FirstName + " " + c.ChangedByUser.LastName,
                    c.Reason,
                    c.FromCategory != null ? c.FromCategory.Name : null,
                    c.ToCategory != null ? c.ToCategory.Name : null,
                    c.FromUrgency,
                    c.ToUrgency,
                    c.FromDepartment != null ? c.FromDepartment.Name : null,
                    c.ToDepartment != null ? c.ToDepartment.Name : null,
                    c.FromUser != null ? c.FromUser.FirstName + " " + c.FromUser.LastName : null,
                    c.ToUser != null ? c.ToUser.FirstName + " " + c.ToUser.LastName : null),
            })
            .ToListAsync(cancellationToken);

        return Merge(
            statuses.Select(s => (s.Item.At, StatusOrder, s.Id, (StaffTimelineItemDto)s.Item)),
            comments.Select(c => (c.Item.At, CommentOrder, c.Id, (StaffTimelineItemDto)c.Item)),
            files.Select(f => (f.CreatedAt, AttachmentOrder, f.Id,
                (StaffTimelineItemDto)new StaffTimelineAttachmentDto(f.CreatedAt, f))),
            changes.Select(c => (c.Item.At, ChangeOrder, c.Id, (StaffTimelineItemDto)c.Item)));
    }

    /// <summary>The ticket through the organization query filter: its children are always reached from it.</summary>
    private IQueryable<Ticket> Ticket(int ticketId) => db.Tickets.AsNoTracking().Where(t => t.Id == ticketId);

    /// <summary>Files attached without a comment (including those of the new-ticket form) – visible to everyone with access.</summary>
    private IQueryable<TicketAttachmentDto> AttachmentsWithoutComment(int ticketId) =>
        Ticket(ticketId)
            .SelectMany(t => t.Attachments)
            .Where(a => a.CommentId == null)
            .Select(a => new TicketAttachmentDto(a.Id, a.OriginalFileName, a.ContentType, a.SizeBytes,
                a.UploadedByUser.FirstName + " " + a.UploadedByUser.LastName, a.CreatedAt));

    /// <summary>
    /// One ordered timeline out of the lists of a single ticket. Only ordering happens here – every visibility rule
    /// was applied in the queries.
    /// </summary>
    private static IReadOnlyList<T> Merge<T>(params IEnumerable<(DateTime At, int Order, int Id, T Item)>[] parts) =>
        parts.SelectMany(p => p)
            .OrderBy(e => e.At).ThenBy(e => e.Order).ThenBy(e => e.Id)
            .Select(e => e.Item)
            .ToList();
}
