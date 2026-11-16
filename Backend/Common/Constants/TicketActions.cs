namespace Common.Constants;

/// <summary>
/// The actions the user may take on one ticket now – the values of <c>AllowedActions</c> of the staff (task 3.4) and the
/// requester's (task 3.5) details. Computed by the server from the permission matrix, the status rules and the
/// assignment; the screen shows only these.
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

    // ===================== Status changes (task 3.5, PATCH /status) =====================

    /// <summary>"התחל טיפול": Assigned → InProgress (from the queue it also assigns the user).</summary>
    public const string Start = "start";

    /// <summary>"בקש מידע": InProgress → WaitingForEmployee, with a message to the employee.</summary>
    public const string RequestInfo = "requestInfo";

    /// <summary>"שלח פתרון": InProgress → Resolved, with the resolution reply.</summary>
    public const string Resolve = "resolve";

    /// <summary>The requester cancels: New / Assigned → Cancelled.</summary>
    public const string Cancel = "cancel";

    /// <summary>The requester confirms ("הבעיה נפתרה"): Resolved → Closed.</summary>
    public const string ConfirmResolved = "confirmResolved";

    /// <summary>The requester reopens ("פתיחה מחדש"): Resolved → InProgress, within 7 days of the resolution.</summary>
    public const string Reopen = "reopen";
}
