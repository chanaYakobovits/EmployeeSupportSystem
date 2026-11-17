using Common.Constants;
using Common.Enums;

namespace Services.Implementations.Ai;

/// <summary>
/// The Hebrew text of an analysis job's error code (task 4.4) – the codes of <see cref="AiErrorCode"/> and
/// <see cref="AiJobErrorCodes"/>. One place, for the handler's screen.
/// </summary>
public static class AiErrorMessages
{
    public const string Skipped = "לא בוצע ניתוח – שירות ה-AI לא היה זמין כשהפנייה נפתחה";
    public const string Unknown = "הניתוח נכשל";

    private static readonly Dictionary<string, string> Messages = new()
    {
        [nameof(AiErrorCode.Timeout)] = "שירות ה-AI לא הגיב בזמן",
        [nameof(AiErrorCode.RateLimited)] = "שירות ה-AI עמוס כרגע",
        [nameof(AiErrorCode.ServerError)] = "תקלה בשירות ה-AI",
        [nameof(AiErrorCode.InvalidResponse)] = "התקבלה משירות ה-AI תשובה לא תקינה",
        [nameof(AiErrorCode.Rejected)] = "שירות ה-AI דחה את הבקשה",
        [nameof(AiErrorCode.Disabled)] = "שירות ה-AI כבוי בהגדרות המערכת",
        [AiJobErrorCodes.FallbackTimeout] = "הניתוח לא הסתיים בזמן, והפנייה נותבה לתור הכללי",
        [AiJobErrorCodes.CircuitOpen] = "שירות ה-AI אינו זמין זמנית",
        [AiJobErrorCodes.RedactionFailed] = "לא ניתן היה לנקות פרטים מזהים מהטקסט, ולכן הוא לא נשלח",
        [AiJobErrorCodes.Abandoned] = "הניתוח נכשל שוב ושוב בעיבוד",
        [AiJobErrorCodes.TicketClosed] = "הפנייה נסגרה לפני שהניתוח בוצע",
        [AiJobErrorCodes.TicketNotFound] = "הפנייה לא נמצאה בזמן הניתוח",
    };

    public static string For(string? errorCode) =>
        errorCode is not null && Messages.TryGetValue(errorCode, out var message) ? message : Unknown;
}
