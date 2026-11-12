using Common.Enums;
using Common.Interfaces;
using Common.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Services.Interfaces;

namespace Services.Implementations.Email;

/// <summary>
/// Writes every message as an .eml file (opens in Outlook / Thunderbird / any text editor) – an alternative to an SMTP
/// server that needs no installation. Development only (enforced at startup): the files hold the invitation link in
/// clear text. The directory is relative to the content root, outside wwwroot and ignored by git.
/// </summary>
public sealed class FileEmailSender(IOptions<EmailOptions> options, IHostEnvironment environment, IDateTimeProvider clock)
    : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var mime = MimeMessageFactory.Create(message, settings);

        try
        {
            var directory = Path.Combine(environment.ContentRootPath, settings.File.Directory);
            Directory.CreateDirectory(directory);
            // Name without any personal data: time + random part.
            var path = Path.Combine(directory, $"{clock.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.eml");
            await mime.WriteToAsync(path, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new EmailSendException(EmailErrorCode.FileWriteFailed, isTransient: true, exception.GetType().Name);
        }
    }
}
