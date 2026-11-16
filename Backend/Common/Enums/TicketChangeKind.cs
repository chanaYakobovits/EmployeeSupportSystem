namespace Common.Enums;

/// <summary>What a <see cref="Models.TicketChange"/> records (task 3.4).</summary>
public enum TicketChangeKind : byte
{
    /// <summary>The category and/or urgency was changed by staff (with a reason); may have routed the ticket again.</summary>
    Reclassified = 1,

    /// <summary>Moved to another handler of the handling department, or to another department's queue.</summary>
    Transferred = 2,

    /// <summary>A handler took a ticket from their department's queue ("קח לטיפולי").</summary>
    Taken = 3
}
