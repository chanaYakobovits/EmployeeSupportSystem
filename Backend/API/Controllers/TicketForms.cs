using Common.DTOs.Tickets;

namespace API.Controllers;

// Multipart forms of the ticket endpoints (spec 9.2). HTTP-only shapes: the controller turns them into the request DTOs
// and IncomingFile, so the Services layer never sees IFormFile. Field names are case-insensitive ("files", "body", ...).

/// <summary>POST /api/tickets. No requester or department field – both come from the token.</summary>
public sealed class CreateTicketForm
{
    public string? Subject { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>Up to 3 files: pdf, jpg, png, docx, up to 5MB each.</summary>
    public List<IFormFile> Files { get; set; } = [];

    public CreateTicketRequest ToRequest() => new() { Subject = Subject, Description = Description };
}

/// <summary>POST /api/tickets/{id}/comments.</summary>
public sealed class CreateCommentForm
{
    public string Body { get; set; } = string.Empty;

    /// <summary>An internal note (handler / department manager only).</summary>
    public bool IsInternal { get; set; }

    public List<IFormFile> Files { get; set; } = [];

    public CreateCommentRequest ToRequest() => new() { Body = Body, IsInternal = IsInternal };
}

/// <summary>POST /api/tickets/{id}/attachments.</summary>
public sealed class AddAttachmentsForm
{
    /// <summary>1–3 files.</summary>
    public List<IFormFile> Files { get; set; } = [];
}

internal static class FormFileExtensions
{
    /// <summary>The name and length are the client's – the services sanitize the name and check the content.</summary>
    public static IReadOnlyList<IncomingFile> ToIncomingFiles(this IEnumerable<IFormFile> files) =>
        files.Select(f => new IncomingFile(f.FileName, f.Length, f.OpenReadStream)).ToList();
}
