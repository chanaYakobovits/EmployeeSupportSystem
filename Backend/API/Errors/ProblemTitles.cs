namespace API.Errors;

/// <summary>
/// Default Hebrew, user-facing ProblemDetails titles by HTTP status code (spec 9.5).
/// Used for every error response, including those produced by the framework itself.
/// </summary>
public static class ProblemTitles
{
    public const string TypeBaseUri = "https://employeesupport/errors/";

    private static readonly Dictionary<int, (string Title, string Type)> Defaults = new()
    {
        [StatusCodes.Status400BadRequest] = ("הבקשה אינה תקינה", "bad-request"),
        [StatusCodes.Status401Unauthorized] = ("נדרשת התחברות למערכת", "unauthorized"),
        [StatusCodes.Status403Forbidden] = ("אין לך הרשאה לבצע פעולה זו", "forbidden"),
        [StatusCodes.Status404NotFound] = ("הפריט המבוקש לא נמצא", "not-found"),
        [StatusCodes.Status405MethodNotAllowed] = ("הפעולה אינה נתמכת", "method-not-allowed"),
        [StatusCodes.Status409Conflict] = ("הנתונים עודכנו בינתיים או שכבר קיימים", "conflict"),
        [StatusCodes.Status413PayloadTooLarge] = ("גודל הבקשה חורג מהמותר", "payload-too-large"),
        [StatusCodes.Status415UnsupportedMediaType] = ("סוג התוכן של הבקשה אינו נתמך", "unsupported-media-type"),
        [StatusCodes.Status422UnprocessableEntity] = ("לא ניתן לבצע את הפעולה", "business-rule"),
        [StatusCodes.Status429TooManyRequests] = ("בוצעו יותר מדי בקשות, יש לנסות שוב בעוד מספר דקות", "too-many-requests"),
        [StatusCodes.Status500InternalServerError] = ("אירעה שגיאה בלתי צפויה", "unexpected"),
    };

    public const string UnexpectedDetail = "יש לנסות שוב מאוחר יותר. אם התקלה חוזרת, יש לפנות לתמיכה עם מזהה התקלה.";

    public const string RequestTooLargeDetail = "ניתן לצרף עד 3 קבצים, עד 5MB כל אחד.";

    /// <summary>413 for a request over the size limit of its endpoint (the files of a ticket or a comment).</summary>
    public static Microsoft.AspNetCore.Mvc.ProblemDetails RequestTooLarge() => new()
    {
        Status = StatusCodes.Status413PayloadTooLarge,
        Type = GetType(StatusCodes.Status413PayloadTooLarge),
        Title = GetTitle(StatusCodes.Status413PayloadTooLarge),
        Detail = RequestTooLargeDetail,
    };

    public static string GetTitle(int statusCode) =>
        Defaults.TryGetValue(statusCode, out var entry) ? entry.Title : "אירעה שגיאה בעיבוד הבקשה";

    public static string GetType(int statusCode) =>
        TypeBaseUri + (Defaults.TryGetValue(statusCode, out var entry) ? entry.Type : "error");
}
