namespace Common.Constants;

/// <summary>
/// <c>AiAnalysisJobs.LastErrorCode</c> values beyond the AI client's <see cref="Enums.AiErrorCode"/> names
/// (Timeout, RateLimited, ServerError, InvalidResponse, Rejected, Disabled). Codes only, never content (spec 8.3).
/// </summary>
public static class AiJobErrorCodes
{
    /// <summary>No result within <c>Ai:FallbackAfterMinutes</c> of the ticket's opening (spec 5.6).</summary>
    public const string FallbackTimeout = "FallbackTimeout";

    /// <summary>The circuit breaker was open when the job came up (spec 5.6).</summary>
    public const string CircuitOpen = "CircuitOpen";

    /// <summary>PII redaction did not complete in time – nothing was sent.</summary>
    public const string RedactionFailed = "RedactionFailed";

    /// <summary>Claimed more often than allowed: earlier attempts crashed without recording a result.</summary>
    public const string Abandoned = "Abandoned";

    /// <summary>The ticket was closed or cancelled before the analysis – nothing was sent.</summary>
    public const string TicketClosed = "TicketClosed";

    /// <summary>The ticket no longer exists in the job's organization.</summary>
    public const string TicketNotFound = "TicketNotFound";
}
