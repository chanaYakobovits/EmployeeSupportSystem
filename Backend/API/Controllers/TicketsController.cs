using API.Extensions;
using Common.DTOs.Paging;
using Common.DTOs.Tickets;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Services.Interfaces;

namespace API.Controllers;

/// <summary>
/// Tickets (spec 9.2). Every signed-in user (default policy). Thin: scope and rules are in <see cref="ITicketService"/>.
/// A ticket outside the user's scope – another user's or another organization's – is not found (404).
/// </summary>
[ApiController]
[Route("api/tickets")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class TicketsController(ITicketService ticketService) : ControllerBase
{
    /// <summary>
    /// Opens a ticket for the signed-in user (JSON; attachments come in task 3.2). Saved immediately with status New;
    /// routing and analysis run in the background. Rate limited: 20 new tickets per hour per user (spec 6.1).
    /// </summary>
    [HttpPost]
    [EnableRateLimiting(AuthExtensions.CreateTicketRateLimitPolicy)]
    [ProducesResponseType<TicketCreatedDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<TicketCreatedDto>> Create(CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        var created = await ticketService.CreateAsync(request, cancellationToken);
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

    /// <summary>A ticket with its status timeline. For now only for its requester (staff scopes: task 3.4).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<RequesterTicketDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequesterTicketDetailsDto>> Get(int id, CancellationToken cancellationToken) =>
        Ok(await ticketService.GetAsync(id, cancellationToken));
}
