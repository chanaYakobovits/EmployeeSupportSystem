namespace Common.Interfaces;

/// <summary>
/// The single source of "now" for business logic, so time-dependent rules (lockout, SLA) can be tested
/// without waiting. Always UTC.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
