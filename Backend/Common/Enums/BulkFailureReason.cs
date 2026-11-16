namespace Common.Enums;

/// <summary>Why one ticket of a bulk update was not updated (spec 11.2, task 3.6).</summary>
public enum BulkFailureReason
{
    /// <summary>The ticket was changed since the version the user saw (409 of the single action).</summary>
    Conflict = 1,

    /// <summary>Missing, of another organization, or outside the user's scope – the reasons are not told apart.</summary>
    NotFound = 2,

    /// <summary>The action is not possible in the ticket's state (status, another handler, not open).</summary>
    InvalidTransition = 3,

    /// <summary>The chosen target does not fit this ticket (e.g. the handler is its requester).</summary>
    InvalidTarget = 4,
}
