using Common.Constants;
using Common.Enums;
using Repositories.Interfaces;
using Services.Interfaces;

namespace Services.Implementations.Routing;

/// <summary>
/// The SLA target of a ticket (spec 3.4), pure: start + the resolution hours of its urgency, in calendar hours.
/// No urgency yet (the fallback, before a handler sets one) → Medium. A level missing from the policies → its default
/// (<see cref="SlaDefaults"/>). The start comes from the caller (the ticket's CreatedAt, from IDateTimeProvider).
/// </summary>
public static class SlaCalculator
{
    /// <summary>The level used while a ticket has no urgency (spec 3.4: "פנייה בתור כללי מקבלת יעד של בינונית").</summary>
    public const Urgency DefaultUrgency = Urgency.Medium;

    public static DateTime DueAt(DateTime start, Urgency? urgency, IReadOnlyDictionary<Urgency, int> hours) =>
        start.AddHours(HoursOf(urgency ?? DefaultUrgency, hours));

    public static int HoursOf(Urgency urgency, IReadOnlyDictionary<Urgency, int> hours) =>
        hours.TryGetValue(urgency, out var value) ? value : SlaDefaults.Hours[urgency];

    /// <summary>
    /// The organization's resolution hours through the reference-data cache (spec 11.3). The organization is explicit:
    /// background jobs have no signed-in user. The repository reads within the scope's organization filter.
    /// </summary>
    public static Task<IReadOnlyDictionary<Urgency, int>> GetHoursAsync(IReferenceDataCache cache,
        ISlaPolicyRepository policies, int organizationId, CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(ReferenceData.SlaPolicies, organizationId, policies.GetHoursAsync, cancellationToken);
}
