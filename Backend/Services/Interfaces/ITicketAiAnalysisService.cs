using Common.DTOs.Tickets;

namespace Services.Interfaces;

/// <summary>The AI analysis of one ticket for staff (task 4.4, spec 9.2): view it, and "נתח מחדש".</summary>
public interface ITicketAiAnalysisService
{
    /// <summary>
    /// The analysis state and the latest stored analysis. Staff with a scope on the ticket (handler, department manager,
    /// executive); the requester – also one who is staff – gets 404, like a ticket outside every scope.
    /// </summary>
    Task<TicketAiAnalysisDto> GetAsync(int ticketId, CancellationToken cancellationToken);

    /// <summary>
    /// Queues a new analysis job ("נתח מחדש"). A handler in scope only (403 otherwise); the ticket must be open, no
    /// analysis in progress and the AI available (422); one job per ticket per
    /// <see cref="Common.Constants.AiRetryLimits.Interval"/> (429).
    /// The result follows the rules of task 4.3: a ticket that is no longer New keeps its classification and routing –
    /// the analysis is a recommendation only.
    /// </summary>
    Task<TicketUpdatedDto> RetryAsync(int ticketId, string ipAddress, CancellationToken cancellationToken);
}
