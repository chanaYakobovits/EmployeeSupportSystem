namespace Common.Constants;

/// <summary>
/// The actions staff may take on one ticket – the values of <c>StaffTicketDetailsDto.AllowedActions</c> (task 3.4).
/// Computed by the server from the permission matrix, the status and the assignment; the screen shows only these.
/// </summary>
public static class TicketActions
{
    /// <summary>A comment the requester sees, or files without a comment.</summary>
    public const string Reply = "reply";

    /// <summary>An internal note (staff only).</summary>
    public const string InternalNote = "internalNote";

    /// <summary>Change the category and/or urgency (PATCH /classification).</summary>
    public const string Reclassify = "reclassify";

    /// <summary>Assign to a handler of the handling department (POST /assign with targetUserId).</summary>
    public const string Assign = "assign";

    /// <summary>Move to another department's queue (POST /assign with targetDepartmentId) – handlers only.</summary>
    public const string TransferToDepartment = "transferToDepartment";

    /// <summary>"קח לטיפולי": a ticket in the queue of the user's own department (POST /assign with the user's id).</summary>
    public const string TakeFromQueue = "takeFromQueue";
}
