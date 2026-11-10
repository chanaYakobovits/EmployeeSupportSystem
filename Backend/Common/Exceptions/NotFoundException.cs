namespace Common.Exceptions;

/// <summary>
/// The requested record does not exist or is outside the caller's permission scope (→ 404).
/// </summary>
public class NotFoundException : AppException
{
    public const string DefaultTitle = "הפריט המבוקש לא נמצא";

    public NotFoundException(string title = DefaultTitle, string? detail = null, string errorType = "not-found")
        : base(title, detail, errorType)
    {
    }
}
