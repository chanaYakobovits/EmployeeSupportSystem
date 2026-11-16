namespace Common.Enums;

/// <summary>
/// The staff lists of GET /api/tickets?scope=... (spec 7.6, 9.2). Which scopes a user may request follows the permission
/// matrix (spec 2.4): the handler's lists for a handler, the department list for a department manager or the executive.
/// </summary>
public enum TicketListScope : byte
{
    /// <summary>"משויכות אליי": assigned to the handler personally.</summary>
    Assigned = 1,

    /// <summary>"תור המחלקה": handled by the handler's department, with no personal handler yet.</summary>
    Queue = 2,

    /// <summary>"כל פניות המחלקה המטפלת": every ticket handled by the handler's department.</summary>
    Handling = 3,

    /// <summary>
    /// A department manager: tickets opened by the department's employees or handled by it. The executive: every ticket
    /// of the organization.
    /// </summary>
    Department = 4
}
