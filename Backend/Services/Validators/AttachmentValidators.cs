using Common.Constants;
using Common.DTOs.Tickets;
using FluentValidation;
using Services.Implementations.Files;

namespace Services.Validators;

/// <summary>
/// The files of one request (spec 6.3, 7.4): at most 3, each non-empty, at most 5MB, an allowed extension and content of
/// that same type. Errors are on "files" (the count) and "files[i]" (one file). Nothing is written before this passes.
/// </summary>
public sealed class AttachmentFilesValidator : AbstractValidator<IReadOnlyList<IncomingFile>>
{
    public const string FieldName = "files";

    public static readonly string TooManyMessage = $"ניתן לצרף עד {AttachmentLimits.MaxFilesPerRequest} קבצים";
    public const string EmptyMessage = "הקובץ ריק";
    public static readonly string TooLargeMessage = $"גודל כל קובץ: עד {AttachmentLimits.MaxFileBytes / (1024 * 1024)}MB";
    public const string TypeNotAllowedMessage = "ניתן לצרף רק קבצי PDF, JPG, PNG או DOCX";
    public const string ContentMismatchMessage = "תוכן הקובץ אינו תואם לסוג הקובץ";

    public AttachmentFilesValidator()
    {
        // Too many files: the files themselves are not read at all.
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(files => files.Count)
            .LessThanOrEqualTo(AttachmentLimits.MaxFilesPerRequest)
            .OverridePropertyName(FieldName)
            .WithMessage(TooManyMessage);

        // Error keys "files[0]", "files[1]", ...: every file is checked, each stops at its own first error.
        RuleForEach(files => files)
            .SetValidator(new FileValidator())
            .OverridePropertyName(FieldName);
    }

    private sealed class FileValidator : AbstractValidator<IncomingFile>
    {
        public FileValidator()
        {
            RuleFor(file => file)
                .Cascade(CascadeMode.Stop)
                .Must(file => file.Length > 0).WithMessage(EmptyMessage)
                .Must(file => file.Length <= AttachmentLimits.MaxFileBytes).WithMessage(TooLargeMessage)
                .Must(file => TypeByName(file) is not null).WithMessage(TypeNotAllowedMessage)
                .MustAsync(ContentMatchesNameAsync).WithMessage(ContentMismatchMessage);
        }
    }

    /// <summary>The allowed type named by the extension of the sanitized name (see <see cref="FileNameSanitizer"/>).</summary>
    private static AttachmentFileType? TypeByName(IncomingFile file) =>
        AttachmentFileTypes.FromExtension(FileNameSanitizer.ExtensionOf(FileNameSanitizer.Sanitize(file.FileName)));

    private static async Task<bool> ContentMatchesNameAsync(IncomingFile file, CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        return await AttachmentFileTypes.DetectAsync(content, cancellationToken) == TypeByName(file);
    }
}

/// <summary>POST /api/tickets/{id}/comments. Length counted after trimming, as the body is stored trimmed.</summary>
public sealed class CreateCommentRequestValidator : AbstractValidator<CreateCommentRequest>
{
    public const string BodyRequiredMessage = "יש לכתוב את התגובה";
    public const string BodyLengthMessage = "התגובה: עד 4,000 תווים";

    public CreateCommentRequestValidator()
    {
        RuleFor(r => r.Body)
            .Cascade(CascadeMode.Stop)
            .Must(body => !string.IsNullOrWhiteSpace(body)).WithMessage(BodyRequiredMessage)
            .Must(body => body.Trim().Length <= CommentFieldLimits.BodyMaxLength).WithMessage(BodyLengthMessage);
    }
}
