using Common.Constants;
using Common.DTOs.Auth;
using Common.Enums;
using Common.Exceptions;
using Common.Models;
using Microsoft.Extensions.Caching.Memory;
using Services.Implementations;
using Services.Tests.Auth.Fakes;
using Services.Tests.Fakes;

namespace Services.Tests.Auth;

public class AuthServiceTests
{
    private const string Password = "Demo1234";
    private const string WrongPassword = "Wrong9999";
    private const string Ip = "10.0.0.1";
    private const string UnknownEmail = "nobody@example.com";

    private static readonly DateTime Start = new(2026, 11, 11, 8, 0, 0, DateTimeKind.Utc);

    private readonly FakeUserRepository _users = new();
    private readonly FakeAuditLogRepository _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeAccessTokenIssuer _tokens = new();
    private readonly FakeDateTimeProvider _clock = new(Start);
    private readonly FakeCurrentUser _currentUser = new();
    private readonly CapturingLogger<AuthService> _logger = new();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _users.Users.Add(CreateUser(4, "EMP-00004", "noa.berkovich@example.com", isActive: true));
        _users.Users.Add(CreateUser(9, "EMP-00009", "tomer.zilber@example.com", isActive: true, mustChangePassword: true));
        _users.Users.Add(CreateUser(12, "EMP-00012", "orit.danon@example.com", isActive: false));

        _service = new AuthService(_users, _audit, _unitOfWork, _hasher, _tokens, _clock,
            new MemoryCache(new MemoryCacheOptions()), _currentUser, _logger);
    }

    private User Noa => _users.Users.Single(u => u.Id == 4);

    private Task<LoginResponse> Login(string identifier, string password) =>
        _service.LoginAsync(new LoginRequest(identifier, password), Ip, CancellationToken.None);

    // ---- identifier detection ----

    [Fact]
    public async Task Identifier_with_at_sign_is_looked_up_by_email()
    {
        await Login("noa.berkovich@example.com", Password);

        Assert.Equal(["email:noa.berkovich@example.com"], _users.Lookups);
    }

    [Fact]
    public async Task Identifier_without_at_sign_is_looked_up_by_employee_code_after_trimming()
    {
        await Login("  EMP-00004  ", Password);

        Assert.Equal(["code:EMP-00004"], _users.Lookups);
    }

    // ---- success ----

    [Fact]
    public async Task Successful_login_returns_token_and_user_and_updates_login_state()
    {
        Noa.FailedLoginCount = 3;

        var response = await Login("EMP-00004", Password);

        Assert.Equal(FakeAccessTokenIssuer.Token, response.AccessToken);
        Assert.False(response.MustChangePassword);
        Assert.Equal(4, response.User.Id);
        Assert.Equal("פיתוח", response.User.Department.Name);
        Assert.Equal(["Employee", "Handler"], response.User.Roles);

        Assert.Equal(0, Noa.FailedLoginCount);
        Assert.Null(Noa.LockoutEnd);
        Assert.Equal(Start, Noa.LastLoginAt);

        var subject = Assert.Single(_tokens.Issued);
        Assert.Equal(new[] { Role.Employee, Role.Handler }, subject.Roles);
        Assert.Equal((4, 1, 2), (subject.UserId, subject.OrganizationId, subject.DepartmentId));

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditActions.LoginSucceeded, entry.Action);
        Assert.Equal((4, 1, 4, Ip), (entry.UserId, entry.OrganizationId, entry.EntityId, entry.IpAddress));
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Successful_login_reports_pending_password_change()
    {
        var response = await Login("tomer.zilber@example.com", Password);

        Assert.True(response.MustChangePassword);
        Assert.True(Assert.Single(_tokens.Issued).MustChangePassword);
    }

    // ---- failure ----

    [Fact]
    public async Task Wrong_password_fails_with_uniform_message_and_counts_the_failure()
    {
        var exception = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Login("EMP-00004", WrongPassword));

        Assert.Equal("פרטי ההתחברות שגויים", exception.Title);
        Assert.Equal(1, Noa.FailedLoginCount);
        Assert.Null(Noa.LastLoginAt);
        Assert.Empty(_tokens.Issued);
        Assert.Equal(AuditActions.LoginFailedWrongPassword, Assert.Single(_audit.Entries).Action);
    }

    [Fact]
    public async Task Inactive_user_with_correct_password_fails_exactly_like_a_wrong_password()
    {
        var inactive = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Login("EMP-00012", Password));
        var wrongPassword = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Login("EMP-00004", WrongPassword));

        AssertSameFailure(wrongPassword, inactive);
        Assert.Equal(2, _hasher.VerifyCount); // the inactive user's password was still verified
        Assert.Empty(_tokens.Issued);
        Assert.Equal(AuditActions.LoginFailedInactive, _audit.Entries[0].Action);
    }

    // ---- lockout ----

    [Fact]
    public async Task Fifth_failure_locks_the_account_for_15_minutes_even_for_the_correct_password()
    {
        await FailTimes("EMP-00004", 4);
        Assert.Null(Noa.LockoutEnd);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Login("EMP-00004", WrongPassword)); // 5th
        Assert.Equal(Start.AddMinutes(15), Noa.LockoutEnd);

        var verifiesBefore = _hasher.VerifyCount;
        var locked = await Assert.ThrowsAsync<AccountLockedException>(() => Login("EMP-00004", Password));

        Assert.Equal(15, locked.RetryAfterMinutes);
        Assert.Contains("15 דקות", locked.Detail);
        Assert.Equal(verifiesBefore, _hasher.VerifyCount); // nothing is verified while locked
        Assert.Equal(AuditActions.LoginBlockedLockedOut, _audit.Entries[^1].Action);
        Assert.Empty(_tokens.Issued);
    }

    [Fact]
    public async Task Lockout_message_shows_the_remaining_minutes_rounded_up()
    {
        await FailTimes("EMP-00004", 5);
        _clock.Advance(TimeSpan.FromMinutes(10).Add(TimeSpan.FromSeconds(30)));

        var locked = await Assert.ThrowsAsync<AccountLockedException>(() => Login("EMP-00004", Password));

        Assert.Equal(5, locked.RetryAfterMinutes);
    }

    [Fact]
    public async Task Account_is_released_after_15_minutes()
    {
        await FailTimes("EMP-00004", 5);

        _clock.Advance(TimeSpan.FromMinutes(15).Subtract(TimeSpan.FromSeconds(1)));
        await Assert.ThrowsAsync<AccountLockedException>(() => Login("EMP-00004", Password));

        _clock.Advance(TimeSpan.FromSeconds(1));
        var response = await Login("EMP-00004", Password);

        Assert.Equal(FakeAccessTokenIssuer.Token, response.AccessToken);
        Assert.Null(Noa.LockoutEnd);
        Assert.Equal(0, Noa.FailedLoginCount);
    }

    // ---- unknown identifier behaves exactly like an existing user ----

    [Fact]
    public async Task Unknown_identifier_fails_like_a_wrong_password_and_still_verifies_a_hash()
    {
        var unknown = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Login(UnknownEmail, Password));
        Assert.Equal(1, _hasher.VerifyCount);

        var wrongPassword = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Login("EMP-00004", WrongPassword));
        AssertSameFailure(wrongPassword, unknown);
    }

    [Fact]
    public async Task Unknown_identifier_is_audited_without_user_organization_or_the_typed_identifier()
    {
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Login(UnknownEmail, Password));

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditActions.LoginFailedUnknownUser, entry.Action);
        Assert.Null(entry.UserId);
        Assert.Null(entry.OrganizationId);
        Assert.Null(entry.EntityId);
        Assert.Equal(Ip, entry.IpAddress);
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.DoesNotContain("nobody", string.Join("|", entry.Action, entry.EntityType, entry.IpAddress));
    }

    [Fact]
    public async Task Unknown_identifier_is_locked_on_the_fifth_failure_exactly_like_an_existing_user()
    {
        await FailTimes(UnknownEmail, 5);
        await FailTimes("EMP-00004", 5);

        var unknownLocked = await Assert.ThrowsAsync<AccountLockedException>(() => Login(UnknownEmail, Password));
        var userLocked = await Assert.ThrowsAsync<AccountLockedException>(() => Login("EMP-00004", Password));

        AssertSameFailure(userLocked, unknownLocked);
        Assert.Equal(15, unknownLocked.RetryAfterMinutes);
    }

    [Fact]
    public async Task Unknown_identifier_lockout_ignores_case_and_surrounding_spaces()
    {
        await FailTimes(UnknownEmail, 5);

        await Assert.ThrowsAsync<AccountLockedException>(() => Login("  NoBody@Example.com ", Password));
    }

    [Fact]
    public async Task Unknown_identifier_is_released_after_15_minutes()
    {
        await FailTimes(UnknownEmail, 5);

        _clock.Advance(TimeSpan.FromMinutes(15).Subtract(TimeSpan.FromSeconds(1)));
        await Assert.ThrowsAsync<AccountLockedException>(() => Login(UnknownEmail, Password));

        _clock.Advance(TimeSpan.FromSeconds(1));
        var afterRelease = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Login(UnknownEmail, Password));
        Assert.IsNotType<AccountLockedException>(afterRelease);
    }

    // ---- logging ----

    [Fact]
    public async Task Log_never_contains_password_hash_token_or_unknown_identifier()
    {
        await Login("EMP-00004", Password);
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Login("EMP-00004", WrongPassword));
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Login(UnknownEmail, WrongPassword));

        var log = _logger.AllText;
        Assert.NotEmpty(_logger.Entries);
        Assert.DoesNotContain(Password, log);
        Assert.DoesNotContain(WrongPassword, log);
        Assert.DoesNotContain(FakePasswordHasher.Prefix, log);
        Assert.DoesNotContain(FakeAccessTokenIssuer.Token, log);
        Assert.DoesNotContain("nobody", log, StringComparison.OrdinalIgnoreCase);
    }

    // ---- me ----

    [Fact]
    public async Task Current_user_is_read_by_the_id_from_ICurrentUser()
    {
        _currentUser.SignedInUserId = 4;

        var me = await _service.GetCurrentUserAsync(CancellationToken.None);

        Assert.Equal("EMP-00004", me.EmployeeCode);
    }

    [Fact]
    public async Task Current_user_that_was_deactivated_is_rejected()
    {
        _currentUser.SignedInUserId = 12;

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => _service.GetCurrentUserAsync(CancellationToken.None));
    }

    // ---- helpers ----

    private async Task FailTimes(string identifier, int times)
    {
        for (var i = 0; i < times; i++)
        {
            await Assert.ThrowsAnyAsync<AuthenticationFailedException>(() => Login(identifier, WrongPassword));
        }
    }

    private static void AssertSameFailure(AuthenticationFailedException expected, AuthenticationFailedException actual)
    {
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.Detail, actual.Detail);
        Assert.Equal(expected.ErrorType, actual.ErrorType);
    }

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
            PasswordHash = _hasher.Hash(Password),
            IsActive = isActive,
            MustChangePassword = mustChangePassword,
        };
        // Deliberately unordered: the service returns roles sorted.
        user.Roles.Add(new UserRole { UserId = id, Role = Role.Handler });
        user.Roles.Add(new UserRole { UserId = id, Role = Role.Employee });
        return user;
    }
}
