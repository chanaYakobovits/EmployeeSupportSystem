using Common.Constants;
using Common.DTOs.Auth;
using Common.Exceptions;
using Common.Interfaces;
using Common.Models;
using Common.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Repositories.Interfaces;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>
/// Login (spec 4.1, 6.1). Every failure looks the same from outside – same exception, same message,
/// one password-hash verification, same lockout after 5 attempts – whether or not the user exists.
/// </summary>
public sealed class AuthService : IAuthService
{
    private const string UnknownIdentifierCacheKeyPrefix = "login-failures:";

    // Verified against when the user does not exist, so the response takes as long as for a real user.
    // Hash of a random value, so no password can ever match it.
    // Static: computed once per process, so the unknown-user path never pays for an extra Hash() call.
    private static string? _dummyHash;

    private readonly IUserRepository _users;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccessTokenIssuer _tokenIssuer;
    private readonly IDateTimeProvider _clock;
    private readonly IMemoryCache _cache;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        IAuditLogRepository auditLogs,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IAccessTokenIssuer tokenIssuer,
        IDateTimeProvider clock,
        IMemoryCache cache,
        ICurrentUser currentUser,
        ILogger<AuthService> logger)
    {
        _users = users;
        _auditLogs = auditLogs;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _tokenIssuer = tokenIssuer;
        _clock = clock;
        _cache = cache;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, string ipAddress, CancellationToken cancellationToken)
    {
        var identifier = request.Identifier.Trim();
        var user = identifier.Contains('@')
            ? await _users.FindForLoginByEmailAsync(identifier, cancellationToken)
            : await _users.FindForLoginByEmployeeCodeAsync(identifier, cancellationToken);

        return user is null
            ? await FailUnknownIdentifierAsync(identifier, request.Password, ipAddress, cancellationToken)
            : await LoginExistingUserAsync(user, request.Password, ipAddress, cancellationToken);
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        // A valid token whose user was deactivated in the meantime is treated as not signed in.
        return await _users.GetCurrentUserAsync(_currentUser.UserId, cancellationToken)
               ?? throw new AuthenticationFailedException(errorType: "unauthorized");
    }

    private async Task<LoginResponse> LoginExistingUserAsync(User user, string password, string ipAddress,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        // While locked, even the correct password is rejected and nothing is verified (same as for unknown identifiers).
        if (user.LockoutEnd is { } lockoutEnd && lockoutEnd > now)
        {
            await AuditAsync(AuditActions.LoginBlockedLockedOut, user, ipAddress, now, cancellationToken);
            _logger.LogWarning("Login blocked: user {UserId} is locked out", user.Id);
            throw new AccountLockedException(lockoutEnd - now);
        }

        // Always verify – also for an inactive user – so timing does not reveal the account state.
        var passwordValid = _passwordHasher.Verify(user.PasswordHash, password);

        if (!passwordValid || !user.IsActive)
        {
            var lockedNow = RegisterFailure(user, now);
            var action = user.IsActive ? AuditActions.LoginFailedWrongPassword : AuditActions.LoginFailedInactive;
            await AuditAsync(action, user, ipAddress, now, cancellationToken);
            _logger.LogWarning("Login failed for user {UserId}: {Action}, locked out: {LockedOut}", user.Id, action, lockedNow);
            throw new AuthenticationFailedException();
        }

        user.FailedLoginCount = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = now;
        await AuditAsync(AuditActions.LoginSucceeded, user, ipAddress, now, cancellationToken);

        var roles = user.Roles.Select(r => r.Role).Order().ToList();
        var token = _tokenIssuer.Issue(new AccessTokenSubject(
            user.Id, user.OrganizationId, user.DepartmentId, roles, user.MustChangePassword));

        _logger.LogInformation("Login succeeded for user {UserId}, must change password: {MustChangePassword}",
            user.Id, user.MustChangePassword);

        var userDto = new CurrentUserDto(
            user.Id,
            user.EmployeeCode,
            user.FirstName,
            user.LastName,
            user.Email,
            user.JobTitle,
            new DepartmentSummaryDto(user.DepartmentId, user.Department.Name),
            roles.Select(r => r.ToString()).ToList(),
            user.MustChangePassword);

        return new LoginResponse(token.Token, token.ExpiresAt, user.MustChangePassword, userDto);
    }

    /// <returns>True if this failure locked the account.</returns>
    private static bool RegisterFailure(User user, DateTime now)
    {
        user.FailedLoginCount++;
        if (user.FailedLoginCount < LoginLockoutOptions.MaxFailedAttempts)
        {
            return false;
        }

        user.LockoutEnd = now + LoginLockoutOptions.LockoutDuration;
        user.FailedLoginCount = 0;
        return true;
    }

    private async Task<LoginResponse> FailUnknownIdentifierAsync(string identifier, string password, string ipAddress,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var key = UnknownIdentifierCacheKeyPrefix + identifier.ToLowerInvariant();
        var state = _cache.Get<UnknownIdentifierState>(key) ?? new UnknownIdentifierState();

        if (state.LockoutEnd is { } lockoutEnd && lockoutEnd > now)
        {
            await AuditAsync(AuditActions.LoginBlockedLockedOut, user: null, ipAddress, now, cancellationToken);
            _logger.LogWarning("Login blocked: unknown identifier is locked out");
            throw new AccountLockedException(lockoutEnd - now);
        }

        _passwordHasher.Verify(GetDummyHash(), password);

        state.FailedCount++;
        if (state.FailedCount >= LoginLockoutOptions.MaxFailedAttempts)
        {
            state.LockoutEnd = now + LoginLockoutOptions.LockoutDuration;
            state.FailedCount = 0;
        }

        // The expiration only frees memory; the lockout itself is decided by IDateTimeProvider above.
        _cache.Set(key, state, new MemoryCacheEntryOptions
        {
            SlidingExpiration = LoginLockoutOptions.LockoutDuration + TimeSpan.FromMinutes(1),
        });

        await AuditAsync(AuditActions.LoginFailedUnknownUser, user: null, ipAddress, now, cancellationToken);
        _logger.LogWarning("Login failed: unknown identifier");
        throw new AuthenticationFailedException();
    }

    private string GetDummyHash() => _dummyHash ??= _passwordHasher.Hash(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));

    private async Task AuditAsync(string action, User? user, string ipAddress, DateTime now,
        CancellationToken cancellationToken)
    {
        // Saved together with the user's counter changes, in one transaction.
        _auditLogs.Add(new AuditLog
        {
            OrganizationId = user?.OrganizationId,
            UserId = user?.Id,
            Action = action,
            EntityType = AuditEntityTypes.User,
            EntityId = user?.Id,
            IpAddress = ipAddress,
            CreatedAt = now,
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private sealed class UnknownIdentifierState
    {
        public int FailedCount { get; set; }
        public DateTime? LockoutEnd { get; set; }
    }
}
