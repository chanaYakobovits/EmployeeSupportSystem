namespace Common.DTOs.Tickets;

/// <summary>
/// POST /api/tickets/{id}/comments (multipart, files apart). The author is the signed-in user. Resolution replies are
/// not created here (task 3.5).
/// </summary>
public sealed class CreateCommentRequest
{
    /// <summary>Required, 1–4,000 characters (after trimming).</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>An internal note, not visible to the requester. Only a handler or department manager with access.</summary>
    public bool IsInternal { get; set; }
}

/// <summary>Response of POST /api/tickets/{id}/comments.</summary>
public sealed record CommentCreatedDto(int Id);
