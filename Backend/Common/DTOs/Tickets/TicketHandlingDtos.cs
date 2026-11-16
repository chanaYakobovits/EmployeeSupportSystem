using Common.Enums;

namespace Common.DTOs.Tickets;

// Handling a ticket – task 3.4: reclassification and transfer by staff (spec 4.4, 9.2).

/// <summary>
/// PATCH /api/tickets/{id}/classification. At least one of <see cref="CategoryId"/> and <see cref="Urgency"/>; a field
/// not sent keeps its value (the urgency cannot be cleared).
/// </summary>
/// <param name="Reason">Required: shown in the staff timeline, never to the requester.</param>
/// <param name="RowVersion">The version the user saw (base64). A newer version → 409.</param>
public sealed record ReclassifyTicketRequest(int? CategoryId, Urgency? Urgency, string Reason, byte[]? RowVersion);

/// <summary>
/// POST /api/tickets/{id}/assign. Exactly one target: a handler of the handling department (the user themselves =
/// "קח לטיפולי"), or another department (its queue).
/// </summary>
/// <param name="Reason">Required, except when a handler takes a ticket from the queue for themselves.</param>
/// <param name="RowVersion">The version the user saw (base64). A newer version → 409.</param>
public sealed record AssignTicketRequest(int? TargetUserId, int? TargetDepartmentId, string? Reason, byte[]? RowVersion);

/// <summary>Response of a change: the new version, and whether the user still has the ticket in scope.</summary>
/// <param name="CanView">False when the change moved the ticket out of the user's scopes (e.g. to another department):
/// the screen goes back to the list instead of reloading the ticket.</param>
public sealed record TicketUpdatedDto(int Id, byte[] RowVersion, bool CanView);
