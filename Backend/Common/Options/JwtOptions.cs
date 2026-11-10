namespace Common.Options;

/// <summary>Access token settings (spec 6.1). Bound from the "Jwt" configuration section.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 requires a key of at least 256 bits.</summary>
    public const int MinSigningKeyBytes = 32;

    /// <summary>
    /// HMAC signing key. Never stored in appsettings.json or git (spec 6.5):
    /// development – <c>dotnet user-secrets set "Jwt:SigningKey" "..." --project API</c>; server – environment variable <c>Jwt__SigningKey</c>.
    /// The application does not start without it.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
}
