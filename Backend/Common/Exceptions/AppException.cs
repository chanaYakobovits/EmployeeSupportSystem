namespace Common.Exceptions;

/// <summary>
/// Base class for expected application errors that are translated to ProblemDetails.
/// <see cref="Title"/> and <see cref="Detail"/> are user-facing (Hebrew) and must not contain
/// sensitive data (ticket text, tokens, etc.), because they are returned to the client.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string title, string? detail, string errorType)
        : base(title)
    {
        Title = title;
        Detail = detail;
        ErrorType = errorType;
    }

    /// <summary>Short user-facing message (Hebrew).</summary>
    public string Title { get; }

    /// <summary>Optional user-facing explanation / next step (Hebrew).</summary>
    public string? Detail { get; }

    /// <summary>Machine-readable error slug, used as the suffix of the ProblemDetails "type" URI.</summary>
    public string ErrorType { get; }
}
