using Common.Enums;

namespace Common.Constants;

/// <summary>Default resolution targets in calendar hours (spec 3.4): used by the seed and for a missing level.</summary>
public static class SlaDefaults
{
    public static readonly IReadOnlyDictionary<Urgency, int> Hours = new Dictionary<Urgency, int>
    {
        [Urgency.Critical] = 4,
        [Urgency.High] = 24,
        [Urgency.Medium] = 72,
        [Urgency.Low] = 120
    };
}
