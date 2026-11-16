using Common.DTOs.Paging;
using Common.DTOs.Tickets;

namespace Services.Interfaces;

/// <summary>Tickets (spec 3, 9.2). The user is always the signed-in one (<see cref="Common.Interfaces.ICurrentUser"/>).</summary>
public interface ITicketService
{
    /// <summary>
    /// Opens a ticket for the signed-in user with up to 3 attachments, and returns its number. Routing and AI run later,
    /// not here. If the database save fails, the files already written are deleted.
    /// </summary>
    /// <exception cref="Common.Exceptions.ValidationException">Invalid subject, description or files.</exception>
    /// <exception cref="Common.Exceptions.AuthenticationFailedException">The user is no longer active.</exception>
    Task<TicketCreatedDto> CreateAsync(CreateTicketRequest request, IReadOnlyList<IncomingFile> files,
        CancellationToken cancellationToken);

    /// <summary>The signed-in user's own tickets ("הפניות שלי").</summary>
    /// <exception cref="Common.Exceptions.ValidationException">Invalid paging, sorting or filter values.</exception>
    Task<PagedResult<RequesterTicketListItemDto>> GetMyPageAsync(MyTicketsQuery query, CancellationToken cancellationToken);

    Task<RequesterTicketsSummaryDto> GetMySummaryAsync(CancellationToken cancellationToken);

    /// <summary>
    /// A ticket within any of the user's viewing scopes (<see cref="ITicketAccess"/>): its requester gets
    /// <see cref="RequesterTicketDetailsDto"/> (even when they are staff too), staff get <see cref="StaffTicketDetailsDto"/>
    /// and the view is recorded in the audit log (spec 6.4).
    /// </summary>
    /// <param name="ipAddress">The client's address, for the audit log.</param>
    /// <exception cref="Common.Exceptions.NotFoundException">Missing or outside the user's scope (spec 2.4: 404, not 403).</exception>
    Task<ITicketDetailsDto> GetAsync(int ticketId, string ipAddress, CancellationToken cancellationToken);

    /// <summary>A staff list (spec 7.6): the scope's tickets, filtered, sorted and paged in SQL.</summary>
    /// <exception cref="Common.Exceptions.ValidationException">Missing scope, invalid paging, sorting or filter values.</exception>
    /// <exception cref="Common.Exceptions.ForbiddenException">The scope is not one of the user's.</exception>
    /// <exception cref="Common.Exceptions.AuthenticationFailedException">The user is no longer active.</exception>
    Task<PagedResult<StaffTicketListItemDto>> GetStaffPageAsync(StaffTicketsQuery query,
        CancellationToken cancellationToken);
}
