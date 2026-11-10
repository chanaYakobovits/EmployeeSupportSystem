using Common.DTOs.Auth;
using Common.Enums;
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

public sealed class FakeAuditLogRepository : IAuditLogRepository
{
    public List<AuditLog> Entries { get; } = [];

    public void Add(AuditLog auditLog) => Entries.Add(auditLog);
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
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

public sealed class FakeCurrentUser : ICurrentUser
{
    public int? SignedInUserId { get; set; }

    public bool IsAuthenticated => SignedInUserId is not null;

    public int UserId => SignedInUserId ?? throw new InvalidOperationException("Not signed in.");

    public int OrganizationId => 1;

    public int DepartmentId => 1;

    public IReadOnlyCollection<Role> Roles => [Role.Employee];

    public bool MustChangePassword => false;

    public bool IsInRole(Role role) => Roles.Contains(role);
}
