using Common.DTOs.Paging;
using Common.DTOs.Tickets;

namespace Services.Interfaces;

/// <summary>Tickets (spec 3, 9.2). The user is always the signed-in one (<see cref="Common.Interfaces.ICurrentUser"/>).</summary>
public interface ITicketService
{
    /// <summary>Opens a ticket for the signed-in user and returns its number. Routing and AI run later, not here.</summary>
    /// <exception cref="Common.Exceptions.ValidationException">Invalid subject or description.</exception>
    /// <exception cref="Common.Exceptions.AuthenticationFailedException">The user is no longer active.</exception>
    Task<TicketCreatedDto> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken);

    /// <summary>The signed-in user's own tickets ("הפניות שלי").</summary>
    /// <exception cref="Common.Exceptions.ValidationException">Invalid paging, sorting or filter values.</exception>
    Task<PagedResult<RequesterTicketListItemDto>> GetMyPageAsync(MyTicketsQuery query, CancellationToken cancellationToken);

    Task<RequesterTicketsSummaryDto> GetMySummaryAsync(CancellationToken cancellationToken);

    /// <summary>A ticket within the user's viewing scope. For now: only tickets the user opened (task 3.4 adds staff).</summary>
    /// <exception cref="Common.Exceptions.NotFoundException">Missing or outside the user's scope (spec 2.4: 404, not 403).</exception>
    Task<RequesterTicketDetailsDto> GetAsync(int ticketId, CancellationToken cancellationToken);
}
