using Common.Enums;

namespace Services.Interfaces;

/// <summary>
/// Sends one e-mail. Implementations: SMTP (MailKit) or .eml files (Development only), chosen by Email:Provider.
/// </summary>
public interface IEmailSender
{
    /// <exception cref="EmailSendException">Sending failed; carries only an error code.</exception>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// A ready message. Contains personal data and, for invitations, the secret link – never logged or stored.
/// </summary>
public sealed record EmailMessage(string ToAddress, string ToName, string Subject, string HtmlBody, string TextBody);

/// <summary>
/// Sending failed. Deliberately carries no inner exception message: SMTP errors can contain the recipient's address.
/// <see cref="IsTransient"/>: worth retrying later (connection, timeout); false when retrying cannot help
/// (the server rejected the recipient).
/// </summary>
public sealed class EmailSendException(EmailErrorCode code, bool isTransient, string exceptionType)
    : Exception($"E-mail sending failed: {code} ({exceptionType})")
{
    public EmailErrorCode Code { get; } = code;

    public bool IsTransient { get; } = isTransient;

    /// <summary>Type name of the original exception, for the log (the type only, never its message).</summary>
    public string ExceptionType { get; } = exceptionType;
}
