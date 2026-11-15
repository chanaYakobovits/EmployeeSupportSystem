using System.IO.Compression;

namespace Services.Implementations.Files;

/// <summary>An allowed attachment type (spec 6.3): pdf, jpg, png, docx.</summary>
/// <param name="ContentType">Stored with the attachment and sent on download – always this value, never the client's.</param>
public sealed record AttachmentFileType(string Name, string ContentType, IReadOnlyList<string> Extensions);

/// <summary>
/// The allowed attachment types and their detection by content ("magic bytes"). A file is accepted only when the type
/// its extension names is the type its content is.
/// </summary>
public static class AttachmentFileTypes
{
    public static readonly AttachmentFileType Pdf = new("PDF", "application/pdf", [".pdf"]);
    public static readonly AttachmentFileType Jpeg = new("JPG", "image/jpeg", [".jpg", ".jpeg"]);
    public static readonly AttachmentFileType Png = new("PNG", "image/png", [".png"]);

    public static readonly AttachmentFileType Docx = new("DOCX",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document", [".docx"]);

    public static readonly IReadOnlyList<AttachmentFileType> All = [Pdf, Jpeg, Png, Docx];

    /// <summary>Content type for a download of anything not recognized (should not happen: types are checked on upload).</summary>
    public const string FallbackContentType = "application/octet-stream";

    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];

    /// <param name="extension">Lower case, with the dot – see <see cref="FileNameSanitizer.ExtensionOf"/>.</param>
    public static AttachmentFileType? FromExtension(string extension) =>
        All.FirstOrDefault(t => t.Extensions.Contains(extension));

    /// <summary>The verified content type if it is one of ours; otherwise <see cref="FallbackContentType"/>.</summary>
    public static string SafeContentType(string storedContentType) =>
        All.Any(t => t.ContentType == storedContentType) ? storedContentType : FallbackContentType;

    /// <summary>The allowed type the content really is, or null. Reads from the current position of the stream.</summary>
    public static async Task<AttachmentFileType?> DetectAsync(Stream content, CancellationToken cancellationToken)
    {
        var header = new byte[PngSignature.Length];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        var start = header.AsSpan(0, read);

        if (start.StartsWith(PdfSignature))
        {
            return Pdf;
        }

        if (start.StartsWith(PngSignature))
        {
            return Png;
        }

        if (start.StartsWith(JpegSignature))
        {
            return Jpeg;
        }

        if (start.StartsWith(ZipSignature))
        {
            return await IsWordDocumentAsync(content, cancellationToken) ? Docx : null;
        }

        return null;
    }

    /// <summary>
    /// A .docx is a ZIP; any ZIP renamed to .docx is not enough. Only the central directory is read (nothing is
    /// decompressed). A macro-enabled document (word/vbaProject.bin) is rejected.
    /// </summary>
    private static async Task<bool> IsWordDocumentAsync(Stream content, CancellationToken cancellationToken)
    {
        Stream seekable = content;
        if (!content.CanSeek)
        {
            // The size was checked before (at most 5MB).
            var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            seekable = copy;
        }

        try
        {
            seekable.Position = 0;
            using var zip = new ZipArchive(seekable, ZipArchiveMode.Read, leaveOpen: true);
            var names = zip.Entries.Select(e => e.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return names.Contains("[Content_Types].xml")
                   && names.Contains("word/document.xml")
                   && !names.Contains("word/vbaProject.bin");
        }
        catch (InvalidDataException)
        {
            return false;
        }
        finally
        {
            if (!ReferenceEquals(seekable, content))
            {
                await seekable.DisposeAsync();
            }
        }
    }
}
