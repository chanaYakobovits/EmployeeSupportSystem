using Common.Options;
using MimeKit;
using Services.Interfaces;

namespace Services.Implementations.Email;

/// <summary>
/// Builds the MIME message (UTF-8, HTML + plain-text alternative). Shared by both senders, so an .eml file is exactly
/// what SMTP would have sent.
/// </summary>
internal static class MimeMessageFactory
{
    public static MimeMessage Create(EmailMessage message, EmailOptions options)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName, message.ToAddress));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }.ToMessageBody();
        return mime;
    }
}
