namespace Common.DTOs.Tickets;

/// <summary>
/// An uploaded file as the Services layer sees it – the API converts IFormFile to this, so the business layer never uses
/// HTTP types. <see cref="FileName"/> and <see cref="Length"/> come from the client and are not trusted: the name is
/// sanitized and the type is detected from the content.
/// </summary>
/// <param name="OpenReadStream">Opens the content from the start; may be called more than once.</param>
public sealed record IncomingFile(string FileName, long Length, Func<Stream> OpenReadStream);
