using Common.DTOs.Auth;
using Common.Enums;
using Common.Models;
using Common.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Services.Implementations;
using Services.Validators;
using Services.Tests.Auth.Fakes;
using Services.Tests.Fakes;

namespace Services.Tests.Auth;

/// <summary>AuthService wired to hand-written fakes, with three demo users.</summary>
public abstract class AuthServiceTestBase
{
    protected const string Password = "Demo1234";
    protected const string WrongPassword = "Wrong9999";
    protected const string Ip = "10.0.0.1";

    protected const int NoaId = 4;
    protected const int TomerId = 9;
    protected const int OritId = 12;

    protected static readonly DateTime Start = new(2026, 11, 11, 8, 0, 0, DateTimeKind.Utc);

    protected readonly FakeUserRepository UserRepository = new();
    protected readonly FakeRefreshTokenRepository RefreshTokens = new();
    protected readonly FakeAuditLogRepository AuditLog = new();
    protected readonly FakeUnitOfWork UnitOfWork = new();
    protected readonly FakePasswordHasher Hasher = new();
    protected readonly FakeAccessTokenIssuer AccessTokens = new();
    protected readonly FakeDateTimeProvider Clock = new(Start);
    protected readonly FakeCurrentUser CurrentUser = new();
    protected readonly CapturingLogger<AuthService> Logger = new();
    protected readonly AuthService Service;

    protected AuthServiceTestBase()
    {
        UserRepository.Users.Add(CreateUser(NoaId, "EMP-00004", "noa.berkovich@example.com", isActive: true));
        UserRepository.Users.Add(CreateUser(TomerId, "EMP-00009", "tomer.zilber@example.com", isActive: true, mustChangePassword: true));
        UserRepository.Users.Add(CreateUser(OritId, "EMP-00012", "orit.danon@example.com", isActive: false));

        Service = new AuthService(UserRepository, RefreshTokens, AuditLog, UnitOfWork, Hasher, AccessTokens, Clock,
            new MemoryCache(new MemoryCacheOptions()), CurrentUser,
            Options.Create(new JwtOptions { RefreshTokenDays = 7 }),
            new LoginRequestValidator(), new ChangePasswordRequestValidator(), Logger);
    }

    protected User Noa => UserById(NoaId);

    protected User UserById(int id) => UserRepository.Users.Single(u => u.Id == id);

    protected Task<AuthResult> Login(string identifier, string password) =>
        Service.LoginAsync(new LoginRequest(identifier, password), Ip, CancellationToken.None);

    protected Task<AuthResult> Refresh(string? refreshToken) =>
        Service.RefreshAsync(refreshToken, Ip, CancellationToken.None);

    protected Task Logout(string? refreshToken) =>
        Service.LogoutAsync(refreshToken, Ip, CancellationToken.None);

    protected Task<AuthResult> ChangePassword(int userId, string current, string newPassword)
    {
        CurrentUser.SignedInUserId = userId;
        return Service.ChangePasswordAsync(new ChangePasswordRequest(current, newPassword), Ip, CancellationToken.None);
    }

    protected RefreshToken StoredToken(string value) =>
        RefreshTokens.Tokens.Single(t => t.TokenHash == SecureTokenGenerator.Hash(value));

    private User CreateUser(int id, string code, string email, bool isActive, bool mustChangePassword = false)
    {
        var user = new User
        {
            Id = id,
            OrganizationId = 1,
            DepartmentId = 2,
            Department = new Department { Id = 2, OrganizationId = 1, Name = "פיתוח" },
            EmployeeCode = code,
            FirstName = "Test",
            LastName = "User",
            Email = email,
            JobTitle = "Developer",
            PasswordHash = Hasher.Hash(Password),
            IsActive = isActive,
            MustChangePassword = mustChangePassword,
        };
        // Deliberately unordered: the service returns roles sorted.
        user.Roles.Add(new UserRole { UserId = id, Role = Role.Handler });
        user.Roles.Add(new UserRole { UserId = id, Role = Role.Employee });
        return user;
    }
}
