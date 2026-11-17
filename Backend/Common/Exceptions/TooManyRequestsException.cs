namespace Common.Exceptions;

/// <summary>
/// A limit the service enforces itself (not the rate limiter middleware), e.g. one re-analysis of a ticket per two
/// minutes (task 4.4) → 429 with <c>Retry-After</c>.
/// </summary>
public class TooManyRequestsException : AppException
{
    public TooManyRequestsException(string title, string? detail, string errorType, TimeSpan retryAfter)
        : base(title, detail, errorType)
    {
        RetryAfter = retryAfter;
    }

    /// <summary>How long until the request may be sent again (rounded up to whole seconds in the header).</summary>
    public TimeSpan RetryAfter { get; }
}
