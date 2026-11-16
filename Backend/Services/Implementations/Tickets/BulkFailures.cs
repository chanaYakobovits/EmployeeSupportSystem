using Common.DTOs.Tickets;
using Common.Enums;
using Common.Exceptions;

namespace Services.Implementations.Tickets;

/// <summary>
/// What the single action threw for one ticket of a bulk update → the reason in the response (spec 11.2, task 3.6).
/// Only the expected outcomes of one ticket; anything else (an inactive user, a database failure) is not a per-ticket
/// result and is not classified.
/// </summary>
public static class BulkFailures
{
    /// <summary>Error types of the single actions that mean "this target does not fit this ticket".</summary>
    public static readonly IReadOnlySet<string> TargetErrorTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "assignee-not-handler",
        "assignee-is-requester",
        "assignment-unchanged",
        "ticket-not-routed",
        "department-has-no-handlers",
    };

    /// <returns>The failure, or null when the exception is not a per-ticket outcome (the caller rethrows it).</returns>
    public static BulkFailureDto? Classify(int ticketId, Exception exception) => exception switch
    {
        // Out of scope (404) or in scope without the right to act (403): neither is told apart (spec 2.4).
        NotFoundException or ForbiddenException => new BulkFailureDto(ticketId, BulkFailureReason.NotFound, null),
        ConflictException { ErrorType: "concurrency" } conflict =>
            new BulkFailureDto(ticketId, BulkFailureReason.Conflict, conflict.ErrorType),
        BusinessRuleException rule => new BulkFailureDto(ticketId,
            TargetErrorTypes.Contains(rule.ErrorType) ? BulkFailureReason.InvalidTarget : BulkFailureReason.InvalidTransition,
            rule.ErrorType),
        // The request itself was validated first; what is left is about the target (e.g. an inactive department).
        ValidationException validation => new BulkFailureDto(ticketId, BulkFailureReason.InvalidTarget,
            validation.ErrorType),
        _ => null,
    };
}
