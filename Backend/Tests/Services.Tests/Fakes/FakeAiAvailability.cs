using Services.Interfaces;

namespace Services.Tests.Fakes;

/// <summary>What ticket creation sees of the AI; set by the test.</summary>
public sealed class FakeAiAvailability : IAiAvailability
{
    public AiAvailability Value { get; set; } = AiAvailability.Disabled;

    public AiAvailability Check() => Value;
}
