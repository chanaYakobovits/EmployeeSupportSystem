namespace Common.Options;

/// <summary>Account lockout after repeated failed logins (spec 4.1, 6.1). Fixed by the spec, not configurable.</summary>
public static class LoginLockoutOptions
{
    public const int MaxFailedAttempts = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
}
