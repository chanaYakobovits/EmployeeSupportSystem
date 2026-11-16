using API.Extensions;
using Common.Constants;
using Common.DTOs.Paging;
using Common.DTOs.Tickets;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Services.Interfaces;

namespace API.Controllers;

/// <summary>
/// Tickets (spec 9.2). Every signed-in user (default policy). Thin: scope and rules are in <see cref="ITicketService"/>
/// and <see cref="ITicketConversationService"/>. A ticket outside the user's scope – another user's or another
/// organization's – is not found (404).
/// The endpoints with files have their own request-size limit (3 × 5MB + the text fields), not the global one;
/// a larger request → 413.
/// </summary>
[ApiController]
[Route("api/tickets")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class TicketsController(
    ITicketService ticketService,
    ITicketConversationService conversationService,
    ITicketHandlingService handlingService) : ControllerBase
{
    /// <summary>
    /// Opens a ticket for the signed-in user (multipart: subject, description, up to 3 files). Saved immediately with
    /// status New; routing and analysis run in the background. Rate limited: 20 new tickets per hour per user (spec 6.1).
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting(AuthExtensions.CreateTicketRateLimitPolicy)]
    [RequestSizeLimit(AttachmentLimits.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = AttachmentLimits.MaxRequestBytes)]
    [ProducesResponseType<TicketCreatedDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<TicketCreatedDto>> Create([FromForm] CreateTicketForm form,
        CancellationToken cancellationToken)
    {
        var created = await ticketService.CreateAsync(form.ToRequest(), form.Files.ToIncomingFiles(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>The signed-in user's own tickets: filter by statuses, search in the subject, sort, page (spec 7.3, 9.4).</summary>
    [HttpGet("my")]
    [ProducesResponseType<PagedResult<RequesterTicketListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<RequesterTicketListItemDto>>> GetMine([FromQuery] MyTicketsQuery query,
        CancellationToken cancellationToken) =>
        Ok(await ticketService.GetMyPageAsync(query, cancellationToken));

    /// <summary>Summary cards: open, waiting for me, closed this month (spec 7.3).</summary>
    [HttpGet("my/summary")]
    [ProducesResponseType<RequesterTicketsSummaryDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RequesterTicketsSummaryDto>> GetMySummary(CancellationToken cancellationToken) =>
        Ok(await ticketService.GetMySummaryAsync(cancellationToken));

    /// <summary>
    /// The staff lists (spec 7.6, 9.4): scope=assigned|queue|handling (handler) or department (department manager,
    /// executive). Filters: status (several), categoryId, urgency (several, NotSet = not set yet), from/to (opening day,
    /// Israel time), overdueOnly, search (subject or requester's name). Default order: the nearest due time first.
    /// A scope that is not the user's → 403.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<StaffTicketListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<StaffTicketListItemDto>>> GetStaffPage(
        [FromQuery] StaffTicketsQuery query, CancellationToken cancellationToken) =>
        Ok(await ticketService.GetStaffPageAsync(query, cancellationToken));

    /// <summary>
    /// A ticket within the user's scope (spec 2.4): <see cref="RequesterTicketDetailsDto"/> for its requester,
    /// <see cref="StaffTicketDetailsDto"/> for staff (recorded in the audit log, spec 6.4).
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<RequesterTicketDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<StaffTicketDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken) =>
        // Serialized as its runtime type (requester or staff DTO).
        Ok(await ticketService.GetAsync(id, HttpContext.ClientIp(), cancellationToken));

    /// <summary>
    /// Reclassification (spec 4.4): category and/or urgency + a reason (staff timeline only) + the version the user saw.
    /// A new category routes the ticket again; the due time is recalculated from the opening.
    /// 403 for staff who may not reclassify; 409 when the ticket changed meanwhile; 422 when resolved, closed or
    /// cancelled, or when nothing changes.
    /// </summary>
    [HttpPatch("{id:int}/classification")]
    [Consumes("application/json")]
    [ProducesResponseType<TicketUpdatedDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TicketUpdatedDto>> Reclassify(int id, ReclassifyTicketRequest request,
        CancellationToken cancellationToken) =>
        Ok(await handlingService.ReclassifyAsync(id, request, cancellationToken));

    /// <summary>
    /// Transfer (spec 4.4): to a handler of the handling department (the user themselves = "קח לטיפולי"), or to another
    /// department's queue (handlers only), with a reason and the version the user saw.
    /// </summary>
    [HttpPost("{id:int}/assign")]
    [Consumes("application/json")]
    [ProducesResponseType<TicketUpdatedDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TicketUpdatedDto>> Assign(int id, AssignTicketRequest request,
        CancellationToken cancellationToken) =>
        Ok(await handlingService.AssignAsync(id, request, cancellationToken));

    /// <summary>
    /// The timeline: status changes, comments and files in order. The requester gets
    /// <see cref="RequesterTicketHistoryDto"/> (no internal notes, their files or reasons); staff get
    /// <see cref="StaffTicketHistoryDto"/>.
    /// </summary>
    [HttpGet("{id:int}/history")]
    [ProducesResponseType<RequesterTicketHistoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<StaffTicketHistoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(int id, CancellationToken cancellationToken) =>
        // Serialized as its runtime type (requester or staff DTO).
        Ok(await conversationService.GetHistoryAsync(id, cancellationToken));

    /// <summary>
    /// A comment or an internal note, with up to 3 files (multipart: body, isInternal, files).
    /// 403 for an internal note by the requester or an executive, or a visible comment by a department manager or an
    /// executive; 422 when the ticket is closed or cancelled.
    /// </summary>
    [HttpPost("{id:int}/comments")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(AttachmentLimits.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = AttachmentLimits.MaxRequestBytes)]
    [ProducesResponseType<CommentCreatedDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CommentCreatedDto>> AddComment(int id, [FromForm] CreateCommentForm form,
        CancellationToken cancellationToken)
    {
        var created = await conversationService.AddCommentAsync(id, form.ToRequest(), form.Files.ToIncomingFiles(),
            cancellationToken);
        // No endpoint of a single comment: it is listed in the history.
        return CreatedAtAction(nameof(GetHistory), new { id }, created);
    }

    /// <summary>Files without a comment (multipart: 1–3 files), visible to the requester. Same rules as a visible comment.</summary>
    [HttpPost("{id:int}/attachments")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(AttachmentLimits.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = AttachmentLimits.MaxRequestBytes)]
    [ProducesResponseType<IReadOnlyList<TicketAttachmentDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyList<TicketAttachmentDto>>> AddAttachments(int id,
        [FromForm] AddAttachmentsForm form, CancellationToken cancellationToken)
    {
        var attachments = await conversationService.AddAttachmentsAsync(id, form.Files.ToIncomingFiles(),
            cancellationToken);
        return CreatedAtAction(nameof(DownloadAttachment), new { id, attachmentId = attachments[0].Id }, attachments);
    }

    /// <summary>
    /// Downloads an attachment, always as a download (Content-Disposition: attachment, the Hebrew name in filename*),
    /// with the verified content type and nosniff. Recorded in the audit log (spec 6.4).
    /// </summary>
    [HttpGet("{id:int}/attachments/{attachmentId:int}")]
    [ProducesResponseType<FileStreamResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAttachment(int id, int attachmentId, CancellationToken cancellationToken)
    {
        var file = await conversationService.DownloadAttachmentAsync(id, attachmentId, HttpContext.ClientIp(),
            cancellationToken);

        // The browser must not guess another type, render the file in the API's origin, or keep a copy.
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "sandbox";
        Response.Headers.CacheControl = "no-store";

        // FileDownloadName → "attachment; filename=...; filename*=UTF-8''..." (RFC 6266). The stream is disposed by MVC.
        return File(file.Content, file.ContentType, file.FileName);
    }
}
