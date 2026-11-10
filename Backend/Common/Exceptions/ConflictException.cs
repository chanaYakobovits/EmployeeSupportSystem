namespace Common.Exceptions;

/// <summary>
/// The request conflicts with the current state, e.g. a concurrent update (RowVersion mismatch)
/// or a duplicate e-mail (→ 409).
/// </summary>
public class ConflictException : AppException
{
    public const string DefaultTitle = "הנתונים עודכנו בינתיים או שכבר קיימים";

    public ConflictException(string title = DefaultTitle, string? detail = null, string errorType = "conflict")
        : base(title, detail, errorType)
    {
    }
}
