using Common.Enums;

namespace Common.DTOs.Tickets;

/// <summary>
/// The signed-in user as the ticket permission matrix needs them (spec 2.4), read from the database – not from the access
/// token, which may be up to 15 minutes old.
/// </summary>
/// <param name="DepartmentId">The user's department: a handler's queue is the queue of this department.</param>
/// <param name="ManagedDepartmentId">The department whose manager the user is (Departments.ManagerUserId), if any.</param>
public sealed record TicketViewer(
    int UserId,
    int DepartmentId,
    int? ManagedDepartmentId,
    IReadOnlyCollection<Role> Roles);

/// <summary>The columns of a ticket that decide who may see and act on it.</summary>
/// <param name="ResolvedAt">For the reopen window (task 3.5).</param>
public sealed record TicketAccessFacts(
    int TicketId,
    int RequesterId,
    int RequesterDepartmentId,
    int? AssignedDepartmentId,
    int? AssignedToUserId,
    TicketStatus Status,
    DateTime? ResolvedAt = null);

/// <summary>A file to download: its content and how to describe it to the browser.</summary>
public sealed record AttachmentDownload(Stream Content, string FileName, string ContentType, long SizeBytes);

/// <summary>Where an attachment's content is stored and how it was described on upload.</summary>
public sealed record StoredAttachmentInfo(int Id, string StoredFileName, string OriginalFileName, string ContentType,
    int SizeBytes);

/// <summary>A ticket and its organization – what a cross-organization background query returns (ids only).</summary>
public sealed record TicketRef(int TicketId, int OrganizationId);
