namespace Common.Enums;

/// <summary>Who may make a status transition (spec 3.3, the "מי" column). Not stored – the history keeps the user id.</summary>
public enum StatusActor
{
    /// <summary>Automatic: routing, the employee's reply, a handler change, the auto-close job.</summary>
    System = 1,

    /// <summary>The ticket's handler.</summary>
    Handler = 2,

    /// <summary>The employee who opened the ticket.</summary>
    Requester = 3
}
