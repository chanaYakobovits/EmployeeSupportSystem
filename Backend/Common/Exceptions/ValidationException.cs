namespace Common.Exceptions;

/// <summary>
/// Invalid input detected by business validation (→ 400, spec 11.4).
/// <see cref="Errors"/> are user-facing (Hebrew) and returned in the ProblemDetails "errors" extension.
/// </summary>
public sealed class ValidationException : AppException
{
    public const string DefaultTitle = "הבקשה אינה תקינה";

    public ValidationException(string title = DefaultTitle, IReadOnlyList<string>? errors = null,
        string? detail = null, string errorType = "validation")
        : base(title, detail, errorType)
    {
        Errors = errors ?? [];
    }

    public IReadOnlyList<string> Errors { get; }
}
