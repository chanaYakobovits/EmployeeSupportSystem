using Common.DTOs.Routing;
using Common.Enums;

namespace Services.Implementations.Routing;

/// <summary>
/// What a ticket's routing needs to know, already loaded (spec 3.5).
/// </summary>
/// <param name="RoutingType">The category's routing type.</param>
/// <param name="HandlingDepartmentId">The category's handling department (FixedDepartment only).</param>
/// <param name="DepartmentHrUserId">HrUserId of the requester's department (RequesterDepartmentHr only).</param>
/// <param name="CentralDepartmentId">Central HR: the handling department of the fallback category (not found by name).</param>
/// <param name="RequesterId">Never assigned their own ticket.</param>
/// <param name="Candidates">
/// Active handlers of the handling department and of central HR, and the departmental HR handler if active with the
/// Handler role – with their load. Anyone not in this list is not available.
/// </param>
public sealed record RoutingInput(
    RoutingType RoutingType,
    int? HandlingDepartmentId,
    int? DepartmentHrUserId,
    int CentralDepartmentId,
    int RequesterId,
    IReadOnlyList<HandlerCandidate> Candidates);

/// <summary>
/// The routing rules of spec 3.5, pure (no I/O, no clock) – <see cref="RoutingService"/> loads the data. The seed
/// (TicketSeedGenerator.Route) mirrors these rules for the sample data.
/// <list type="bullet">
/// <item>FixedDepartment → the category's handling department.</item>
/// <item>RequesterDepartmentHr → the HR handler of the requester's department, in the HR handler's own department
/// (so the other handlers of the requester's department never see it in their queue). Missing or unavailable → central HR.</item>
/// <item>Within a department: never the requester; one handler → them; several → the fewest unresolved tickets,
/// a tie → the lowest id. No one → central HR; no one there either → the central HR queue (no handler).</item>
/// </list>
/// </summary>
public static class RoutingRules
{
    public static RoutingDecision Decide(RoutingInput input)
    {
        if (input.RoutingType == RoutingType.RequesterDepartmentHr)
        {
            if (input.DepartmentHrUserId is not { } hrUserId)
            {
                return Central(input, RoutingReason.DepartmentHrMissing);
            }

            var hr = input.Candidates.FirstOrDefault(c => c.UserId == hrUserId);
            return hr is not null && hr.UserId != input.RequesterId
                ? new RoutingDecision(hr.DepartmentId, hr.UserId, RoutingReason.DepartmentHr)
                : Central(input, RoutingReason.DepartmentHrUnavailable);
        }

        // FixedDepartment. A category without a department cannot be saved (CategoryService); central HR just in case.
        if (input.HandlingDepartmentId is not { } departmentId)
        {
            return Central(input, RoutingReason.NoHandlerInDepartment);
        }

        var handler = PickHandler(input.Candidates, departmentId, input.RequesterId);
        if (handler is not null)
        {
            return new RoutingDecision(departmentId, handler, RoutingReason.FixedDepartment);
        }

        // The fallback category routes to central HR itself: its queue is where the ticket stays.
        return departmentId == input.CentralDepartmentId
            ? new RoutingDecision(departmentId, null, RoutingReason.FixedDepartment)
            : Central(input, RoutingReason.NoHandlerInDepartment);
    }

    /// <summary>
    /// Simple load balancing (spec 3.5): the department's handler with the fewest unresolved tickets, a tie → the lowest
    /// id, never the requester. Null if the department has no one else.
    /// </summary>
    public static int? PickHandler(IReadOnlyList<HandlerCandidate> candidates, int departmentId, int requesterId) =>
        candidates
            .Where(c => c.DepartmentId == departmentId && c.UserId != requesterId)
            .OrderBy(c => c.OpenTicketCount)
            .ThenBy(c => c.UserId)
            .Select(c => (int?)c.UserId)
            .FirstOrDefault();

    /// <summary>Central HR: a handler there if one is available, otherwise its queue.</summary>
    private static RoutingDecision Central(RoutingInput input, RoutingReason reason) =>
        new(input.CentralDepartmentId, PickHandler(input.Candidates, input.CentralDepartmentId, input.RequesterId), reason);
}
