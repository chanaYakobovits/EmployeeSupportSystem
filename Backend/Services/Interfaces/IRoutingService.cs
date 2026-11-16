using Common.DTOs.Routing;

namespace Services.Interfaces;

/// <summary>
/// Routing of a ticket (spec 3.5): which department handles it and which handler gets it. The organization is
/// explicit, so the AI worker (4.3), which has no signed-in user, routes the same way as a request.
/// </summary>
public interface IRoutingService
{
    /// <summary>The organization's active fallback category (GENERAL, IsFallback), or null if there is none.</summary>
    Task<int?> GetFallbackCategoryIdAsync(int organizationId, CancellationToken cancellationToken);

    /// <summary>
    /// Routes a ticket of this category opened by this requester. Categories and departments come from the
    /// reference-data cache; the handlers and their load from the database.
    /// </summary>
    /// <exception cref="InvalidOperationException">The category does not exist, or there is no fallback category.</exception>
    Task<RoutingDecision> RouteAsync(int organizationId, int categoryId, int requesterId, int requesterDepartmentId,
        CancellationToken cancellationToken);
}
