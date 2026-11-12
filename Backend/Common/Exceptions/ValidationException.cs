namespace Common.Exceptions;

/// <summary>
/// Invalid input (→ 400, spec 11.4). <see cref="AppException.Errors"/> holds the messages by field name.
/// </summary>
public sealed class ValidationException : AppException
{
    public const string DefaultTitle = "הבקשה אינה תקינה";

    public ValidationException(string title = DefaultTitle, IReadOnlyDictionary<string, string[]>? errors = null,
        string? detail = null, string errorType = "validation")
        : base(title, detail, errorType, errors)
    {
    }

    /// <summary>A single field error with the default title.</summary>
    public static ValidationException ForField(string field, string message) =>
        new(errors: new Dictionary<string, string[]> { [field] = [message] });
}
