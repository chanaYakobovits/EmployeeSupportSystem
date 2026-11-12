using System.Globalization;
using System.Net;
using System.Text;
using Common.Models;
using Common.Options;
using Microsoft.Extensions.Options;
using Services.Interfaces;

namespace Services.Implementations.Invitations;

/// <summary>
/// The invitation e-mail: Hebrew, right-to-left HTML (embedded template) + a plain-text alternative.
/// Name, employee code, link and expiry – never a password. Every value is HTML-encoded (names are user input).
/// The token goes after '#' in the link, so it is never sent to a server in a URL (logs, Referer, proxies).
/// </summary>
public sealed class InvitationEmailComposer(IOptions<InvitationOptions> options)
{
    public const string Subject = "הזמנה לעובדנט – קביעת סיסמה";

    private static readonly Lazy<string> HtmlTemplate = new(LoadTemplate);
    private static readonly TimeZoneInfo IsraelTime = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");

    public EmailMessage Compose(User user, string token, DateTime expiresAtUtc)
    {
        var link = BuildLink(options.Value.FrontendBaseUrl, token);
        var expires = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc), IsraelTime)
            .ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

        var html = HtmlTemplate.Value
            .Replace("{{FirstName}}", WebUtility.HtmlEncode(user.FirstName))
            .Replace("{{EmployeeCode}}", WebUtility.HtmlEncode(user.EmployeeCode))
            .Replace("{{Link}}", WebUtility.HtmlEncode(link))
            .Replace("{{ExpiresAt}}", WebUtility.HtmlEncode(expires));

        var text = new StringBuilder()
            .AppendLine($"שלום {user.FirstName},")
            .AppendLine()
            .AppendLine("נפתח עבורך חשבון במערכת עובדנט לניהול פניות עובדים.")
            .AppendLine($"קוד העובד שלך: {user.EmployeeCode}")
            .AppendLine()
            .AppendLine("כדי להתחיל, יש לקבוע סיסמה דרך הקישור הבא:")
            .AppendLine(link)
            .AppendLine()
            .AppendLine($"הקישור תקף עד {expires} (שעון ישראל), ואפשר להשתמש בו פעם אחת בלבד.")
            .AppendLine()
            .AppendLine("אם לא ציפית למייל הזה – אפשר להתעלם ממנו.")
            .ToString();

        return new EmailMessage(user.Email, $"{user.FirstName} {user.LastName}", Subject, html, text);
    }

    /// <summary><c>{base}/set-password#token={token}</c>. The token is Base64Url, so it needs no escaping.</summary>
    public static string BuildLink(string frontendBaseUrl, string token) =>
        $"{frontendBaseUrl.TrimEnd('/')}/set-password#token={token}";

    private static string LoadTemplate()
    {
        var assembly = typeof(InvitationEmailComposer).Assembly;
        // Not "invitation.he.html": MSBuild would treat ".he" as a culture and move it into a satellite assembly.
        const string suffix = ".Templates.invitation.html";
        var resourceName = assembly.GetManifestResourceNames().SingleOrDefault(n => n.EndsWith(suffix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Embedded e-mail template '*{suffix}' was not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
