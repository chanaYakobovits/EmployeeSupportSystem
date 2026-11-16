using Common.Enums;
using Services.Implementations.Routing;

namespace Services.Tests.Routing;

/// <summary>SLA targets (spec 3.4): start + the urgency's calendar hours; no urgency → Medium.</summary>
public sealed class SlaCalculatorTests
{
    private static readonly DateTime Start = new(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);

    private static readonly Dictionary<Urgency, int> Policies = new()
    {
        [Urgency.Critical] = 2,
        [Urgency.High] = 10,
        [Urgency.Medium] = 30,
        [Urgency.Low] = 100,
    };

    [Theory]
    [InlineData(Urgency.Critical, 2)]
    [InlineData(Urgency.High, 10)]
    [InlineData(Urgency.Medium, 30)]
    [InlineData(Urgency.Low, 100)]
    public void Each_urgency_uses_its_hours_from_the_policies(Urgency urgency, int hours)
    {
        Assert.Equal(Start.AddHours(hours), SlaCalculator.DueAt(Start, urgency, Policies));
    }

    [Fact]
    public void No_urgency_uses_medium()
    {
        Assert.Equal(Start.AddHours(30), SlaCalculator.DueAt(Start, urgency: null, Policies));
    }

    [Fact]
    public void A_level_missing_from_the_policies_uses_its_default()
    {
        var withoutLow = Policies.Where(p => p.Key != Urgency.Low).ToDictionary();

        Assert.Equal(Start.AddHours(120), SlaCalculator.DueAt(Start, Urgency.Low, withoutLow));
        Assert.Equal(Start.AddHours(72), SlaCalculator.DueAt(Start, urgency: null, new Dictionary<Urgency, int>()));
    }

    [Fact]
    public void Calendar_hours_run_through_nights_and_weekends()
    {
        // Friday 18:00 UTC + 24h → Saturday 18:00 (working hours are a future extension, spec 3.4).
        var friday = new DateTime(2026, 11, 13, 18, 0, 0, DateTimeKind.Utc);

        Assert.Equal(friday.AddDays(1), SlaCalculator.DueAt(friday, Urgency.High,
            new Dictionary<Urgency, int> { [Urgency.High] = 24 }));
    }
}
