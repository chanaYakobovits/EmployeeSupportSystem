namespace Common.Options;

/// <summary>
/// Outgoing e-mail (invitation e-mails). Bound from the "Email" configuration section.
/// <see cref="SmtpSettings.Password"/> is never stored in appsettings.json or git:
/// development – <c>dotnet user-secrets set "Email:Smtp:Password" "..."</c>; server – environment variable
/// <c>Email__Smtp__Password</c>.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// <see cref="EmailProvider.File"/> writes every message as an .eml file (no installation needed) and is allowed only
    /// in Development – the files contain the invitation link in clear text.
    /// </summary>
    public EmailProvider Provider { get; set; } = EmailProvider.File;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "עובדנט";

    public SmtpSettings Smtp { get; set; } = new();

    public FileSettings File { get; set; } = new();

    public sealed class SmtpSettings
    {
        public string Host { get; set; } = string.Empty;

        public int Port { get; set; } = 25;

        /// <summary>None (smtp4dev), StartTls (port 587), SslOnConnect (port 465) or Auto.</summary>
        public SmtpSecurity Security { get; set; } = SmtpSecurity.StartTls;

        /// <summary>Empty = no authentication (smtp4dev).</summary>
        public string? UserName { get; set; }

        /// <summary>Only in user-secrets / environment variables.</summary>
        public string? Password { get; set; }

        public int TimeoutSeconds { get; set; } = 30;
    }

    public sealed class FileSettings
    {
        /// <summary>Relative to the API's content root; outside wwwroot and git-ignored.</summary>
        public string Directory { get; set; } = "App_Data/emails";
    }
}

public enum EmailProvider
{
    Smtp,
    File,
}

public enum SmtpSecurity
{
    None,
    StartTls,
    SslOnConnect,
    Auto,
}
