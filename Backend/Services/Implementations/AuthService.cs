using Common.Constants;
using Common.DTOs.Auth;
using Common.Exceptions;
using Common.Interfaces;
using Common.Models;
using Common.Options;
using FluentValidation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repositories.Interfaces;
using Services.Interfaces;
using Services.Validators;
using ValidationException = Common.Exceptions.ValidationException;

namespace Services.Implementations;

/// <summary>
/// Login, refresh-token rotation, logout and password change (spec 4.1, 6.1, 7.2).
/// Login: every failure looks the same from outside – same exception, same message, one password-hash
/// verification, same lockout after 5 attempts – whether or not the user exists.
/// Never logs passwords, hashes or token values; only user ids, actions and results.
/// </summary>
public sealed class AuthService : IAuthService
{
    /// <summary>
    /// A token revoked by rotation less than this long ago is not treated as reuse: two tabs refreshing at the
    /// same moment both send it, and only one can win. After it, a rotated token presented again means reuse
    /// (all sessions revoked). Tokens revoked any other way (logout, password change) are always just rejected.
    /// </summary>
    public static readonly TimeSpan RotationGracePeriod = TimeSpan.FromSeconds(30);

    private const string UnknownIdentifierCacheKeyPrefix = "login-failures:";

    // Verified against when the user does not exist or has no password yet (invited), so the response takes as long
    // as for a real user.
    // Hash of a random value, so no password can ever match it.
    // Static: computed once per process, so the unknown-user path never pays for an extra Hash() call.
    private static string? _dummyHash;

    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccessTokenIssuer _tokenIssuer;
    private readonly IDateTimeProvider _clock;
    private readonly IMemoryCache _cache;
    private readonly ICurrentUser _currentUser;
    private readonly JwtOptions _jwtOptions;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<ChangePasswordRequest> _changePasswordValidator;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IAuditLogRepository auditLogs,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IAccessTokenIssuer tokenIssuer,
        IDateTimeProvider clock,
        IMemoryCache cache,
        ICurrentUser currentUser,
        IOptions<JwtOptions> jwtOptions,
        IValidator<LoginRequest> loginValidator,
        IValidator<ChangePasswordRequest> changePasswordValidator,
        ILogger<AuthService> logger)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _auditLogs = auditLogs;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _tokenIssuer = tokenIssuer;
        _clock = clock;
        _cache = cache;
        _currentUser = currentUser;
        _jwtOptions = jwtOptions.Value;
        _loginValidator = loginValidator;
        _changePasswordValidator = changePasswordValidator;
        _logger = logger;
    }

    // ===================== Login =====================

    public async Task<AuthResult> LoginAsync(LoginRequest request, string ipAddress, CancellationToken cancellationToken)
    {
        await _loginValidator.EnsureValidAsync(request, cancellationToken);

        var identifier = request.Identifier.Trim();
        var user = identifier.Contains('@')
            ? await _users.FindForLoginByEmailAsync(identifier, cancellationToken)
            : await _users.FindForLoginByEmployeeCodeAsync(identifier, cancellationToken);

        return user is null
            ? await FailUnknownIdentifierAsync(identifier, request.Password, ipAddress, cancellationToken)
            : await LoginExistingUserAsync(user, request.Password, ipAddress, cancellationToken);
    }

    private async Task<AuthResult> LoginExistingUserAsync(User user, string password, string ipAddress,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        // While locked, even the correct password is rejected and nothing is verified (same as for unknown identifiers).
        if (user.LockoutEnd is { } lockoutEnd && lockoutEnd > now)
        {
            Audit(AuditActions.LoginBlockedLockedOut, user, ipAddress, now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("Login blocked: user {UserId} is locked out", user.Id);
            throw new AccountLockedException(lockoutEnd - now);
        }

        // Always verify – also for an inactive user, or an invited one without a password (against the dummy hash) –
        // so timing does not reveal the account state.
        var passwordSet = user.PasswordHash is not null;
        var passwordValid = _passwordHasher.Verify(user.PasswordHash ?? GetDummyHash(), password) && passwordSet;

        if (!passwordValid || !user.IsActive)
        {
            var lockedNow = RegisterFailure(user, now);
            var action = !user.IsActive ? AuditActions.LoginFailedInactive
                : !passwordSet ? AuditActions.LoginFailedPasswordNotSet
                : AuditActions.LoginFailedWrongPassword;
            Audit(action, user, ipAddress, now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("Login failed for user {UserId}: {Action}, locked out: {LockedOut}", user.Id, action, lockedNow);
            throw new AuthenticationFailedException();
        }

        user.FailedLoginCount = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = now;
        Audit(AuditActions.LoginSucceeded, user, ipAddress, now);
        var result = StartSession(user, now);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Login succeeded for user {UserId}, must change password: {MustChangePassword}",
            user.Id, user.MustChangePassword);
        return result;
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

    private async Task<AuthResult> FailUnknownIdentifierAsync(string identifier, string password, string ipAddress,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var key = UnknownIdentifierCacheKeyPrefix + identifier.ToLowerInvariant();
        var state = _cache.Get<UnknownIdentifierState>(key) ?? new UnknownIdentifierState();

        if (state.LockoutEnd is { } lockoutEnd && lockoutEnd > now)
        {
            Audit(AuditActions.LoginBlockedLockedOut, user: null, ipAddress, now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
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

        Audit(AuditActions.LoginFailedUnknownUser, user: null, ipAddress, now);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogWarning("Login failed: unknown identifier");
        throw new AuthenticationFailedException();
    }

    private string GetDummyHash() => _dummyHash ??= _passwordHasher.Hash(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));

    // ===================== Refresh =====================

    public async Task<AuthResult> RefreshAsync(string? refreshToken, string ipAddress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw SessionExpired();
        }

        var now = _clock.UtcNow;
        var stored = await _refreshTokens.FindByHashAsync(SecureTokenGenerator.Hash(refreshToken), cancellationToken);
        if (stored is null)
        {
            _logger.LogWarning("Refresh failed: unknown token");
            throw SessionExpired();
        }

        var user = stored.User;

        if (stored.RevokedAt is { } revokedAt)
        {
            if (!WasRotated(stored))
            {
                // Revoked by logout, password change or a revoke-all: there is no successor an attacker could hold,
                // so revoking everything would only sign out the real user (e.g. an old phone after a password change).
                _logger.LogInformation("Refresh rejected for user {UserId}: token was revoked (logout / password change)", user.Id);
                throw SessionExpired();
            }

            if (now - revokedAt < RotationGracePeriod)
            {
                // Concurrent refresh from another tab: not an attack, nothing else is revoked.
                _logger.LogInformation("Refresh rejected for user {UserId}: token rotated moments ago", user.Id);
                throw SessionExpired();
            }

            // A rotated token was presented again after the grace period: whoever holds its successor may be an
            // attacker who stole the cookie. Kill every session of the user.
            var revokedCount = await RevokeAllAsync(user.Id, now, cancellationToken);
            Audit(AuditActions.RefreshTokenReuseDetected, user, ipAddress, now);
            await SaveIgnoringConflictAsync(cancellationToken);
            _logger.LogWarning("Refresh token reuse detected for user {UserId}; revoked {Count} active tokens",
                user.Id, revokedCount);
            throw SessionExpired();
        }

        if (stored.ExpiresAt <= now)
        {
            _logger.LogInformation("Refresh failed for user {UserId}: token expired", user.Id);
            throw SessionExpired();
        }

        if (!user.IsActive)
        {
            await RevokeAllAsync(user.Id, now, cancellationToken);
            Audit(AuditActions.RefreshFailedInactive, user, ipAddress, now);
            await SaveIgnoringConflictAsync(cancellationToken);
            _logger.LogWarning("Refresh failed for user {UserId}: user is inactive; tokens revoked", user.Id);
            throw SessionExpired();
        }

        var result = StartSession(user, now, out var replacement);
        stored.RevokedAt = now;
        stored.ReplacedByToken = replacement;

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictException)
        {
            // Another request rotated the same token between our read and our write (RevokedAt concurrency token).
            // Nothing was saved; treated like the grace-period case.
            _logger.LogInformation("Refresh rejected for user {UserId}: concurrent rotation", user.Id);
            throw SessionExpired();
        }

        _logger.LogInformation("Refresh token rotated for user {UserId}", user.Id);
        return result;
    }

    private static bool WasRotated(RefreshToken token) =>
        token.ReplacedByTokenId is not null || token.ReplacedByToken is not null;

    // ===================== Logout =====================

    public async Task LogoutAsync(string? refreshToken, string ipAddress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var now = _clock.UtcNow;
        var stored = await _refreshTokens.FindByHashAsync(SecureTokenGenerator.Hash(refreshToken), cancellationToken);
        if (stored is null || stored.RevokedAt is not null || stored.ExpiresAt <= now)
        {
            // Already invalid: logging out is still a success for the caller.
            _logger.LogInformation("Logout with an inactive or unknown refresh token");
            return;
        }

        stored.RevokedAt = now;
        Audit(AuditActions.Logout, stored.User, ipAddress, now);
        await SaveIgnoringConflictAsync(cancellationToken);
        _logger.LogInformation("Logout for user {UserId}", stored.UserId);
    }

    // ===================== Change password =====================

    public async Task<AuthResult> ChangePasswordAsync(ChangePasswordRequest request, string ipAddress,
        CancellationToken cancellationToken)
    {
        await _changePasswordValidator.EnsureValidAsync(request, cancellationToken);

        var user = await _users.FindForUpdateAsync(_currentUser.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new AuthenticationFailedException(AuthenticationFailedException.NotSignedInTitle, errorType: "unauthorized");
        }

        var now = _clock.UtcNow;

        // A user without a password cannot be signed in, so this is only defensive.
        if (user.PasswordHash is null || !_passwordHasher.Verify(user.PasswordHash, request.CurrentPassword))
        {
            Audit(AuditActions.PasswordChangeFailedWrongCurrent, user, ipAddress, now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("Password change failed for user {UserId}: wrong current password", user.Id);
            throw new ValidationException("הסיסמה הנוכחית שגויה",
                new Dictionary<string, string[]> { ["currentPassword"] = ["הסיסמה הנוכחית שגויה"] },
                errorType: "wrong-current-password");
        }

        var errors = PasswordPolicy.Validate(request.NewPassword, request.CurrentPassword);
        if (errors.Count > 0)
        {
            Audit(AuditActions.PasswordChangeFailedPolicy, user, ipAddress, now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Password change failed for user {UserId}: policy ({ErrorCount} errors)", user.Id, errors.Count);
            throw new ValidationException("הסיסמה החדשה אינה עומדת בדרישות",
                new Dictionary<string, string[]> { ["newPassword"] = errors.ToArray() }, errorType: "password-policy");
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.MustChangePassword = false;
        var revokedCount = await RevokeAllAsync(user.Id, now, cancellationToken);
        Audit(AuditActions.PasswordChanged, user, ipAddress, now);
        var result = StartSession(user, now);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password changed for user {UserId}; revoked {Count} refresh tokens", user.Id, revokedCount);
        return result;
    }

    // ===================== Me =====================

    public async Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        // A valid token whose user was deactivated in the meantime is treated as not signed in.
        return await _users.GetCurrentUserAsync(_currentUser.UserId, cancellationToken)
               ?? throw new AuthenticationFailedException(AuthenticationFailedException.NotSignedInTitle, errorType: "unauthorized");
    }

    // ===================== Helpers =====================

    private AuthResult StartSession(User user, DateTime now) => StartSession(user, now, out _);

    /// <summary>Stages a new refresh token for the user and issues an access token. The caller saves.</summary>
    private AuthResult StartSession(User user, DateTime now, out RefreshToken refreshTokenEntity)
    {
        var value = SecureTokenGenerator.NewValue();
        var expiresAt = now.AddDays(_jwtOptions.RefreshTokenDays);
        refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            User = user,
            TokenHash = SecureTokenGenerator.Hash(value),
            CreatedAt = now,
            ExpiresAt = expiresAt,
        };
        _refreshTokens.Add(refreshTokenEntity);

        var roles = user.Roles.Select(r => r.Role).Order().ToList();
        var accessToken = _tokenIssuer.Issue(new AccessTokenSubject(
            user.Id, user.OrganizationId, user.DepartmentId, roles, user.MustChangePassword));

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

        return new AuthResult(
            new AuthResponse(accessToken.Token, accessToken.ExpiresAt, user.MustChangePassword, userDto),
            new IssuedRefreshToken(value, expiresAt));
    }

    /// <returns>Number of tokens revoked (staged; the caller saves).</returns>
    private async Task<int> RevokeAllAsync(int userId, DateTime now, CancellationToken cancellationToken)
    {
        var active = await _refreshTokens.GetActiveForUserAsync(userId, now, cancellationToken);
        foreach (var token in active)
        {
            token.RevokedAt = now;
        }

        return active.Count;
    }

    /// <summary>
    /// For revocations: a conflict means another request revoked/rotated a token at the same moment.
    /// Nothing is saved then; the caller still rejects the request.
    /// </summary>
    private async Task SaveIgnoringConflictAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictException)
        {
            _logger.LogWarning("Concurrent update while revoking refresh tokens; nothing saved");
        }
    }

    private static AuthenticationFailedException SessionExpired() =>
        new(AuthenticationFailedException.SessionExpiredTitle, errorType: "session-expired");

    private void Audit(string action, User? user, string ipAddress, DateTime now) =>
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

    private sealed class UnknownIdentifierState
    {
        public int FailedCount { get; set; }
        public DateTime? LockoutEnd { get; set; }
    }
}
