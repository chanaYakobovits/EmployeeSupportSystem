using Common.Options;
using Microsoft.Extensions.Options;
using Services.Implementations.Ai;
using Services.Interfaces;
using Services.Tests.Fakes;

namespace Services.Tests.Ai;

/// <summary>Spec 5.6: 5 consecutive failures → no AI calls for 5 minutes.</summary>
public sealed class AiCircuitBreakerTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);

    private readonly FakeDateTimeProvider _clock = new(Now);
    private readonly AiOptions _options = new() { Enabled = true, ApiKey = "test-key" };
    private readonly AiCircuitBreaker _breaker;

    public AiCircuitBreakerTests()
    {
        _breaker = new AiCircuitBreaker(_clock, Options.Create(_options), new CapturingLogger<AiCircuitBreaker>());
    }

    private void Fail(int times)
    {
        for (var i = 0; i < times; i++)
        {
            _breaker.RecordFailure();
        }
    }

    [Fact]
    public void Opens_after_five_consecutive_failures()
    {
        Fail(4);
        Assert.False(_breaker.IsOpen);

        Fail(1);
        Assert.True(_breaker.IsOpen);
    }

    [Fact]
    public void A_success_resets_the_count()
    {
        Fail(4);
        _breaker.RecordSuccess();
        Fail(4);

        Assert.False(_breaker.IsOpen);
    }

    [Fact]
    public void Closes_after_five_minutes()
    {
        Fail(5);

        _clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        Assert.True(_breaker.IsOpen);

        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(_breaker.IsOpen);
    }

    [Fact]
    public void A_failed_trial_after_the_open_period_opens_it_again_at_once()
    {
        Fail(5);
        _clock.Advance(TimeSpan.FromMinutes(5));

        Fail(1);

        Assert.True(_breaker.IsOpen);
        _clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        Assert.True(_breaker.IsOpen);
    }

    [Fact]
    public void A_successful_trial_closes_it_for_good()
    {
        Fail(5);
        _clock.Advance(TimeSpan.FromMinutes(5));

        _breaker.RecordSuccess();
        Fail(4);

        Assert.False(_breaker.IsOpen);
    }

    [Fact]
    public void Availability_is_CircuitOpen_while_the_breaker_is_open()
    {
        var availability = new AiAvailabilityCheck(Options.Create(_options), _breaker);
        Assert.Equal(AiAvailability.Available, availability.Check());

        Fail(5);
        Assert.Equal(AiAvailability.CircuitOpen, availability.Check());

        _clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(AiAvailability.Available, availability.Check());
    }

    [Theory]
    [InlineData(false, "test-key")]
    [InlineData(true, null)]
    [InlineData(true, " ")]
    public void Availability_is_Disabled_without_Enabled_or_a_key(bool enabled, string? apiKey)
    {
        var options = new AiOptions { Enabled = enabled, ApiKey = apiKey };

        Assert.Equal(AiAvailability.Disabled, new AiAvailabilityCheck(Options.Create(options), _breaker).Check());
    }
}
