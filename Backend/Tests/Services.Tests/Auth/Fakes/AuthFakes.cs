using Common.DTOs.Auth;
using Common.Enums;
using Common.Exceptions;
using Common.Interfaces;
using Common.Models;
using Repositories.Interfaces;
using Services.Interfaces;

namespace Services.Tests.Auth.Fakes;

/// <summary>In-memory users. Records which lookup the service used.</summary>
public sealed class FakeUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public List<string> Lookups { get; } = [];

    public Task<User?> FindForLoginByEmailAsync(string email, CancellationToken cancellationToken)
    {
        Lookups.Add($"email:{email}");
        return Task.FromResult(Users.SingleOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<User?> FindForLoginByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken)
    {
        Lookups.Add($"code:{employeeCode}");
        return Task.FromResult(Users.SingleOrDefault(u => string.Equals(u.EmployeeCode, employeeCode, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<User?> FindForUpdateAsync(int userId, CancellationToken cancellationToken) =>
        Task.FromResult(Users.SingleOrDefault(u => u.Id == userId));

    public Task<CurrentUserDto?> GetCurrentUserAsync(int userId, CancellationToken cancellationToken)
    {
        var user = Users.SingleOrDefault(u => u.Id == userId && u.IsActive);
        return Task.FromResult(user is null
            ? null
            : new CurrentUserDto(user.Id, user.EmployeeCode, user.FirstName, user.LastName, user.Email, user.JobTitle,
                new DepartmentSummaryDto(user.DepartmentId, user.Department.Name),
                user.Roles.Select(r => r.Role.ToString()).ToList(), user.MustChangePassword));
    }
}

/// <summary>In-memory refresh tokens. Assigns ids on Add, like the database would on insert.</summary>
public sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
{
    private int _nextId = 1;

    public List<RefreshToken> Tokens { get; } = [];

    public void Add(RefreshToken refreshToken)
    {
        refreshToken.Id = _nextId++;
        Tokens.Add(refreshToken);
    }

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Tokens.SingleOrDefault(t => t.TokenHash == tokenHash));

    public Task<IReadOnlyList<RefreshToken>> GetActiveForUserAsync(int userId, DateTime now, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RefreshToken>>(
            Tokens.Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now).ToList());

    public IEnumerable<RefreshToken> ActiveFor(int userId, DateTime now) =>
        Tokens.Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now);
}

public sealed class FakeAuditLogRepository : IAuditLogRepository
{
    public List<AuditLog> Entries { get; } = [];

    public void Add(AuditLog auditLog) => Entries.Add(auditLog);
}

/// <summary>
/// Counts saves. <see cref="ConflictOnNextSave"/> simulates a concurrency conflict (another request changed the row);
/// <see cref="BeforeConflict"/> lets the test undo staged changes, as a failed database transaction would.
/// </summary>
public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public bool ConflictOnNextSave { get; set; }

    public Action? BeforeConflict { get; set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (ConflictOnNextSave)
        {
            ConflictOnNextSave = false;
            BeforeConflict?.Invoke();
            throw new ConflictException(errorType: "concurrency");
        }

        SaveCount++;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Transparent "hash" (no crypto, so tests are fast). Counts Verify calls, so a test can check that the
/// unknown-user path verifies against a dummy hash exactly like the real-user path.
/// </summary>
public sealed class FakePasswordHasher : IPasswordHasher
{
    public const string Prefix = "HASH:";

    public int VerifyCount { get; private set; }

    public string Hash(string password) => Prefix + password;

    public bool Verify(string hashedPassword, string providedPassword)
    {
        VerifyCount++;
        return hashedPassword == Prefix + providedPassword;
    }
}

public sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
{
    public const string Token = "fake-access-token";

    public List<AccessTokenSubject> Issued { get; } = [];

    public AccessToken Issue(AccessTokenSubject subject)
    {
        Issued.Add(subject);
        return new AccessToken(Token, new DateTime(2030, 1, 1, 0, 15, 0, DateTimeKind.Utc));
    }
}

/// <summary>Signed in when <see cref="SignedInUserId"/> is set; organization configurable.</summary>
public sealed class FakeCurrentUser : ICurrentUser
{
    public int? SignedInUserId { get; set; }

    public int SignedInOrganizationId { get; set; } = 1;

    public bool IsAuthenticated => SignedInUserId is not null;

    public int UserId => SignedInUserId ?? throw new InvalidOperationException("Not signed in.");

    public int OrganizationId => IsAuthenticated ? SignedInOrganizationId : throw new InvalidOperationException("Not signed in.");

    public int DepartmentId => 1;

    public IReadOnlyCollection<Role> Roles => IsAuthenticated ? [Role.Employee] : [];

    public bool MustChangePassword => false;

    public bool IsInRole(Role role) => Roles.Contains(role);
}
