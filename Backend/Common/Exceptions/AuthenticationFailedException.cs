namespace Common.Exceptions;

/// <summary>
/// Login failed or the caller is not (or no longer) a valid user (→ 401).
/// The default message is identical for every cause – unknown user, wrong password, inactive user –
/// so the response never reveals whether a user exists (spec 4.1).
/// </summary>
public class AuthenticationFailedException : AppException
{
    public const string DefaultTitle = "פרטי ההתחברות שגויים";

    /// <summary>Refresh token missing, expired, revoked or reused – the user must sign in again.</summary>
    public const string SessionExpiredTitle = "פג תוקף ההתחברות, יש להתחבר מחדש";

    /// <summary>A valid access token whose user no longer exists or is inactive.</summary>
    public const string NotSignedInTitle = "נדרשת התחברות למערכת";

    public AuthenticationFailedException(string title = DefaultTitle, string? detail = null, string errorType = "invalid-credentials")
        : base(title, detail, errorType)
    {
    }
}
