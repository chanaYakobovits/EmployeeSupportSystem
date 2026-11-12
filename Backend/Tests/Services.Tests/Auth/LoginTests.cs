using Common.Constants;
using Common.Enums;
using Common.Exceptions;
using Services.Implementations;
using Services.Tests.Auth.Fakes;

namespace Services.Tests.Auth;

public class LoginTests : AuthServiceTestBase
{
    private const string UnknownEmail = "nobody@example.com";

    // ---- identifier detection ----

    [Fact]
    public async Task Identifier_with_at_sign_is_looked_up_by_email()
    {
        await Login("noa.berkovich@example.com", Password);

        Assert.Equal(["email:noa.berkovich@example.com"], UserRepository.Lookups);
    }

    [Fact]
    public async Task Identifier_without_at_sign_is_looked_up_by_employee_code_after_trimming()
    {
        await Login("  EMP-00004  ", Password);

        Assert.Equal(["code:EMP-00004"], UserRepository.Lookups);
    }

    // ---- success ----

    [Fact]
    public async Task Successful_login_returns_token_and_user_and_updates_login_state()
    {
        Noa.FailedLoginCount = 3;

        var result = await Login("EMP-00004", Password);
        var response = result.Body;

        Assert.Equal(FakeAccessTokenIssuer.Token, response.AccessToken);
        Assert.False(response.MustChangePassword);
        Assert.Equal(NoaId, response.User.Id);
        Assert.Equal("פיתוח", response.User.Department.Name);
        Assert.Equal(["Employee", "Handler"], response.User.Roles);

        Assert.Equal(0, Noa.FailedLoginCount);
        Assert.Null(Noa.LockoutEnd);
        Assert.Equal(Start, Noa.LastLoginAt);

        var subject = Assert.Single(AccessTokens.Issued);
        Assert.Equal(new[] { Role.Employee, Role.Handler }, subject.Roles);
        Assert.Equal((NoaId, 1, 2), (subject.UserId, subject.OrganizationId, subject.DepartmentId));

        var entry = Assert.Single(AuditLog.Entries);
        Assert.Equal(AuditActions.LoginSucceeded, entry.Action);
        Assert.Equal((NoaId, 1, NoaId, Ip), (entry.UserId, entry.OrganizationId, entry.EntityId, entry.IpAddress));
        Assert.Equal(1, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task Successful_login_issues_a_7_day_refresh_token_stored_only_as_hash()
    {
        var result = await Login("EMP-00004", Password);

        Assert.Equal(Start.AddDays(7), result.RefreshToken.ExpiresAt);
        Assert.True(result.RefreshToken.Value.Length >= 43); // 32 random bytes, base64url

        var stored = Assert.Single(RefreshTokens.Tokens);
        Assert.Equal(SecureTokenGenerator.Hash(result.RefreshToken.Value), stored.TokenHash);
        Assert.NotEqual(result.RefreshToken.Value, stored.TokenHash);
        Assert.Equal((NoaId, Start, Start.AddDays(7)), (stored.UserId, stored.CreatedAt, stored.ExpiresAt));
        Assert.Null(stored.RevokedAt);
    }

    [Fact]
    public async Task Each_login_gets_a_different_refresh_token()
    {
        var first = await Login("EMP-00004", Password);
        var second = await Login("EMP-00004", Password);

        Assert.NotEqual(first.RefreshToken.Value, second.RefreshToken.Value);
    }

    [Fact]
    public async Task Successful_login_reports_pending_password_change()
    {
        var result = await Login("tomer.zilber@example.com", Password);

        Assert.True(result.Body.MustChangePassword);
        Assert.True(Assert.Single(AccessTokens.Issued).MustChangePassword);
    }

    // ---- failure ----

    [Fact]
    public async Task Wrong_password_fails_with_uniform_message_and_counts_the_failure()
    {
        var exception = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Login("EMP-00004", WrongPassword));

        Assert.Equal("פרטי ההתחברות שגויים", exception.Title);
        Assert.Equal(1, Noa.FailedLoginCount);
        Assert.Null(Noa.LastLoginAt);
        Assert.Empty(AccessTokens.Issued);
        Assert.Empty(RefreshTokens.Tokens);
        Assert.Equal(AuditActions.LoginFailedWrongPassword, Assert.Single(AuditLog.Entries).Action);
    }

    [Fact]
    public async Task Inactive_user_with_correct_password_fails_exactly_like_a_wrong_password()
    {
        var inactive = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Login("EMP-00012", Password));
        var wrongPassword = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Login("EMP-00004", WrongPassword));

        AssertSameFailure(wrongPassword, inactive);
        Assert.Equal(2, Hasher.VerifyCount); // the inactive user's password was still verified
        Assert.Empty(AccessTokens.Issued);
        Assert.Equal(AuditActions.LoginFailedInactive, AuditLog.Entries[0].Action);
    }

    // ---- lockout ----

    [Fact]
    public async Task Fifth_failure_locks_the_account_for_15_minutes_even_for_the_correct_password()
    {
        await FailTimes("EMP-00004", 4);
        Assert.Null(Noa.LockoutEnd);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Login("EMP-00004", WrongPassword)); // 5th
        Assert.Equal(Start.AddMinutes(15), Noa.LockoutEnd);

        var verifiesBefore = Hasher.VerifyCount;
        var locked = await Assert.ThrowsAsync<AccountLockedException>(() => Login("EMP-00004", Password));

        Assert.Equal(15, locked.RetryAfterMinutes);
        Assert.Contains("15 דקות", locked.Detail);
        Assert.Equal(verifiesBefore, Hasher.VerifyCount); // nothing is verified while locked
        Assert.Equal(AuditActions.LoginBlockedLockedOut, AuditLog.Entries[^1].Action);
        Assert.Empty(AccessTokens.Issued);
    }

    [Fact]
    public async Task Lockout_message_shows_the_remaining_minutes_rounded_up()
    {
        await FailTimes("EMP-00004", 5);
        Clock.Advance(TimeSpan.FromMinutes(10).Add(TimeSpan.FromSeconds(30)));

        var locked = await Assert.ThrowsAsync<AccountLockedException>(() => Login("EMP-00004", Password));

        Assert.Equal(5, locked.RetryAfterMinutes);
    }

    [Fact]
    public async Task Account_is_released_after_15_minutes()
    {
        await FailTimes("EMP-00004", 5);

        Clock.Advance(TimeSpan.FromMinutes(15).Subtract(TimeSpan.FromSeconds(1)));
        await Assert.ThrowsAsync<AccountLockedException>(() => Login("EMP-00004", Password));

        Clock.Advance(TimeSpan.FromSeconds(1));
        var result = await Login("EMP-00004", Password);

        Assert.Equal(FakeAccessTokenIssuer.Token, result.Body.AccessToken);
        Assert.Null(Noa.LockoutEnd);
        Assert.Equal(0, Noa.FailedLoginCount);
    }

    // ---- unknown identifier behaves exactly like an existing user ----

    [Fact]
    public async Task Unknown_identifier_fails_like_a_wrong_password_and_still_verifies_a_hash()
    {
        var unknown = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Login(UnknownEmail, Password));
        Assert.Equal(1, Hasher.VerifyCount);

        var wrongPassword = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Login("EMP-00004", WrongPassword));
        AssertSameFailure(wrongPassword, unknown);
    }

    [Fact]
    public async Task Unknown_identifier_is_audited_without_user_organization_or_the_typed_identifier()
    {
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Login(UnknownEmail, Password));

        var entry = Assert.Single(AuditLog.Entries);
        Assert.Equal(AuditActions.LoginFailedUnknownUser, entry.Action);
        Assert.Null(entry.UserId);
        Assert.Null(entry.OrganizationId);
        Assert.Null(entry.EntityId);
        Assert.Equal(Ip, entry.IpAddress);
        Assert.Equal(1, UnitOfWork.SaveCount);
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

        Clock.Advance(TimeSpan.FromMinutes(15).Subtract(TimeSpan.FromSeconds(1)));
        await Assert.ThrowsAsync<AccountLockedException>(() => Login(UnknownEmail, Password));

        Clock.Advance(TimeSpan.FromSeconds(1));
        var afterRelease = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Login(UnknownEmail, Password));
        Assert.IsNotType<AccountLockedException>(afterRelease);
    }

    // ---- logging ----

    [Fact]
    public async Task Log_never_contains_password_hash_tokens_or_unknown_identifier()
    {
        var result = await Login("EMP-00004", Password);
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Login("EMP-00004", WrongPassword));
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Login(UnknownEmail, WrongPassword));

        var log = Logger.AllText;
        Assert.NotEmpty(Logger.Entries);
        Assert.DoesNotContain(Password, log);
        Assert.DoesNotContain(WrongPassword, log);
        Assert.DoesNotContain(FakePasswordHasher.Prefix, log);
        Assert.DoesNotContain(FakeAccessTokenIssuer.Token, log);
        Assert.DoesNotContain(result.RefreshToken.Value, log);
        Assert.DoesNotContain(SecureTokenGenerator.Hash(result.RefreshToken.Value), log);
        Assert.DoesNotContain("nobody", log, StringComparison.OrdinalIgnoreCase);
    }

    // ---- me ----

    [Fact]
    public async Task Current_user_is_read_by_the_id_from_ICurrentUser()
    {
        CurrentUser.SignedInUserId = NoaId;

        var me = await Service.GetCurrentUserAsync(CancellationToken.None);

        Assert.Equal("EMP-00004", me.EmployeeCode);
    }

    [Fact]
    public async Task Current_user_that_was_deactivated_is_rejected()
    {
        CurrentUser.SignedInUserId = OritId;

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Service.GetCurrentUserAsync(CancellationToken.None));
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
}
