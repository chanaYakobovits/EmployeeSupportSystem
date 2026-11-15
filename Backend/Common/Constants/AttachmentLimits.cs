namespace Common.Constants;

/// <summary>Attachment limits (spec 6.3, 7.4). Used by the validators, the storage and the request-size limits of the API.</summary>
public static class AttachmentLimits
{
    public const int MaxFileBytes = 5 * 1024 * 1024;

    public const int MaxFilesPerRequest = 3;

    /// <summary>
    /// Limit of a whole multipart request with files: <see cref="MaxFilesPerRequest"/> × <see cref="MaxFileBytes"/> plus
    /// room for the text fields and multipart headers. Set per endpoint, not as the global Kestrel limit.
    /// </summary>
    public const int MaxRequestBytes = 16 * 1024 * 1024;

    /// <summary>Maximum length of a stored original file name, after sanitizing (the column allows 255).</summary>
    public const int MaxFileNameLength = 120;
}

/// <summary>Limits of a ticket comment (spec 8.3).</summary>
public static class CommentFieldLimits
{
    public const int BodyMaxLength = 4000;
}
