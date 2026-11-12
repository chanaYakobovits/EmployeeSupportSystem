namespace Common.Exceptions;

/// <summary>
/// Base class for expected application errors that are translated to ProblemDetails.
/// <see cref="Title"/>, <see cref="Detail"/> and <see cref="Errors"/> are user-facing (Hebrew) and must not contain
/// sensitive data (ticket text, tokens, passwords), because they are returned to the client.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string title, string? detail, string errorType,
        IReadOnlyDictionary<string, string[]>? errors = null)
        : base(title)
    {
        Title = title;
        Detail = detail;
        ErrorType = errorType;
        Errors = errors ?? new Dictionary<string, string[]>();
    }

    /// <summary>Short user-facing message (Hebrew).</summary>
    public string Title { get; }

    /// <summary>Optional user-facing explanation / next step (Hebrew).</summary>
    public string? Detail { get; }

    /// <summary>Machine-readable error slug, used as the suffix of the ProblemDetails "type" URI.</summary>
    public string ErrorType { get; }

    /// <summary>
    /// User-facing messages by field name (camelCase, as in the JSON body), returned in the ProblemDetails "errors"
    /// extension so the client can show them next to the fields. Empty when the error is not about specific fields.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
