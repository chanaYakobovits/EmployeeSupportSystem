using Common.DTOs.Tickets;
using Common.Models;

namespace Repositories.Interfaces;

/// <summary>
/// Comments, attachments and the timeline of a ticket (task 3.2). None of these methods checks the viewing scope – the
/// caller does that first with TicketAccess. What the requester may not see is left out by the queries themselves.
/// Every query starts from the (organization-filtered) ticket, like the other ticket children.
/// </summary>
public interface ITicketConversationRepository
{
    /// <summary>Stages the comment (with its attachments); written by <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
    void AddComment(TicketComment comment);

    /// <summary>Stages an attachment without a comment.</summary>
    void AddAttachment(TicketAttachment attachment);

    /// <summary>
    /// The attachment if it belongs to the ticket and – unless <paramref name="includeInternal"/> – is not a file of an
    /// internal note. Otherwise null, exactly like a missing one.
    /// </summary>
    Task<StoredAttachmentInfo?> GetAttachmentAsync(int ticketId, int attachmentId, bool includeInternal,
        CancellationToken cancellationToken);

    /// <summary>Attachments of the ticket with these ids, as listed in a timeline, ordered by id.</summary>
    Task<IReadOnlyList<TicketAttachmentDto>> GetAttachmentDtosAsync(int ticketId, IReadOnlyCollection<int> attachmentIds,
        CancellationToken cancellationToken);

    /// <summary>The requester's timeline: no internal notes (nor their files), no status-change reasons.</summary>
    Task<IReadOnlyList<RequesterTimelineItemDto>> GetRequesterTimelineAsync(int ticketId, CancellationToken cancellationToken);

    /// <summary>The full timeline, for staff with access.</summary>
    Task<IReadOnlyList<StaffTimelineItemDto>> GetStaffTimelineAsync(int ticketId, CancellationToken cancellationToken);
}
