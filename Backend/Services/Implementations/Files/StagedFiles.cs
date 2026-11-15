using Common.Constants;
using Common.DTOs.Tickets;
using Common.Exceptions;
using Common.Interfaces;
using Common.Models;
using Microsoft.Extensions.Logging;
using Services.Validators;

namespace Services.Implementations.Files;

/// <summary>
/// Files written for one request, deleted again unless the database save succeeded. Usage:
/// <c>await using var staged = new StagedFiles(...)</c>; stage the attachments; SaveChanges; <see cref="Commit"/>.
/// Disposing without a commit (validation error, database failure, cancellation) deletes what was written.
/// Call only after the files were validated (<see cref="AttachmentFilesValidator"/>).
/// </summary>
internal sealed class StagedFiles(IFileStorage storage, ILogger logger) : IAsyncDisposable
{
    private readonly List<string> _written = [];
    private bool _committed;

    /// <summary>Writes the file and returns its attachment row (TicketId and CommentId are set by the caller).</summary>
    public async Task<TicketAttachment> SaveAttachmentAsync(IncomingFile file, int uploadedByUserId, DateTime now,
        CancellationToken cancellationToken)
    {
        var fileName = FileNameSanitizer.Sanitize(file.FileName);
        var type = AttachmentFileTypes.FromExtension(FileNameSanitizer.ExtensionOf(fileName))
                   ?? throw new InvalidOperationException("Attachments must be validated before they are stored.");

        StoredFile stored;
        await using (var content = file.OpenReadStream())
        {
            try
            {
                stored = await storage.SaveAsync(content, AttachmentLimits.MaxFileBytes, cancellationToken);
            }
            catch (InvalidDataException)
            {
                // The declared length was within the limit but the content was not.
                throw ValidationException.ForField("files", AttachmentFilesValidator.TooLargeMessage);
            }
        }

        _written.Add(stored.Name);
        return new TicketAttachment
        {
            OriginalFileName = fileName,
            StoredFileName = stored.Name,
            ContentType = type.ContentType,
            SizeBytes = (int)stored.SizeBytes,
            UploadedByUserId = uploadedByUserId,
            CreatedAt = now,
        };
    }

    /// <summary>The database has the rows: keep the files.</summary>
    public void Commit() => _committed = true;

    public async ValueTask DisposeAsync()
    {
        if (_committed)
        {
            return;
        }

        foreach (var name in _written)
        {
            try
            {
                await storage.DeleteAsync(name);
            }
            catch (Exception exception)
            {
                // Best effort: an orphan file is left behind. The stored name is random – no personal data in the log.
                logger.LogWarning("Could not delete staged attachment file {StoredFileName}: {ErrorType}",
                    name, exception.GetType().Name);
            }
        }

        if (_written.Count > 0)
        {
            logger.LogInformation("Deleted {FileCount} staged attachment files after a failed save", _written.Count);
        }
    }
}
