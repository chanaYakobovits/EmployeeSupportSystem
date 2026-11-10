namespace Common.Exceptions;

/// <summary>
/// The request is well-formed but violates a business rule, e.g. a forbidden status transition (→ 422).
/// </summary>
public class BusinessRuleException : AppException
{
    public const string DefaultTitle = "לא ניתן לבצע את הפעולה";

    public BusinessRuleException(string title = DefaultTitle, string? detail = null, string errorType = "business-rule")
        : base(title, detail, errorType)
    {
    }
}
