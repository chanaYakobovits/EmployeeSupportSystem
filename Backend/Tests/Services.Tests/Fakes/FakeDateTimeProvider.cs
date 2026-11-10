using Common.Interfaces;

namespace Services.Tests.Fakes;

/// <summary>A clock that only moves when the test says so.</summary>
public sealed class FakeDateTimeProvider : IDateTimeProvider
{
    public FakeDateTimeProvider(DateTime utcNow) => UtcNow = utcNow;

    public DateTime UtcNow { get; private set; }

    public void Advance(TimeSpan by) => UtcNow += by;
}
