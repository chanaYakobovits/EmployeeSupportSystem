using System.Net.Sockets;
using Common.Enums;
using Common.Options;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using Services.Interfaces;

namespace Services.Implementations.Email;

/// <summary>
/// SMTP through MailKit: smtp4dev in development (Security = None, port 25), a real server later – configuration only.
/// One connection per message (invitations are rare). Errors become <see cref="EmailSendException"/> codes; the
/// original message is dropped because it can contain the recipient's address.
/// </summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var smtp = settings.Smtp;
        using var mime = MimeMessageFactory.Create(message, settings);
        using var client = new SmtpClient { Timeout = smtp.TimeoutSeconds * 1000 };

        try
        {
            await client.ConnectAsync(smtp.Host, smtp.Port, ToSocketOptions(smtp.Security), cancellationToken);
            if (!string.IsNullOrEmpty(smtp.UserName))
            {
                await client.AuthenticateAsync(smtp.UserName, smtp.Password ?? string.Empty, cancellationToken);
            }

            await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is not EmailSendException)
        {
            throw Translate(exception);
        }
    }

    internal static EmailSendException Translate(Exception exception)
    {
        var type = exception.GetType().Name;
        return exception switch
        {
            // The server refused this recipient for good (5xx): retrying cannot help.
            SmtpCommandException { ErrorCode: SmtpErrorCode.RecipientNotAccepted } command when (int)command.StatusCode >= 500
                => new EmailSendException(EmailErrorCode.RecipientRejected, isTransient: false, type),
            // Wrong credentials are a configuration problem: retried, so a fix within the retry window still delivers.
            AuthenticationException => new EmailSendException(EmailErrorCode.SmtpAuthenticationFailed, isTransient: true, type),
            TimeoutException or OperationCanceledException
                => new EmailSendException(EmailErrorCode.Timeout, isTransient: true, type),
            SocketException or SslHandshakeException or ProtocolException or IOException or ServiceNotConnectedException
                => new EmailSendException(EmailErrorCode.SmtpConnectionFailed, isTransient: true, type),
            _ => new EmailSendException(EmailErrorCode.Unknown, isTransient: true, type),
        };
    }

    private static SecureSocketOptions ToSocketOptions(SmtpSecurity security) => security switch
    {
        SmtpSecurity.None => SecureSocketOptions.None,
        SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
        SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
        _ => SecureSocketOptions.Auto,
    };
}
