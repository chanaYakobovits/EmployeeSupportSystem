namespace Common.Exceptions;

/// <summary>
/// The user may see the record but not perform this action on it, e.g. an employee writing an internal note (→ 403).
/// Records outside the user's scope are <see cref="NotFoundException"/> (404) instead – spec 2.4.
/// </summary>
public class ForbiddenException : AppException
{
    public const string DefaultTitle = "אין לך הרשאה לבצע פעולה זו";

    public ForbiddenException(string title = DefaultTitle, string? detail = null, string errorType = "forbidden")
        : base(title, detail, errorType)
    {
    }
}
