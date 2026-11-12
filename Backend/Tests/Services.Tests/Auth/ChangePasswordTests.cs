using Common.Constants;
using Common.Exceptions;
using Services.Implementations;
using Services.Tests.Auth.Fakes;

namespace Services.Tests.Auth;

public class ChangePasswordTests : AuthServiceTestBase
{
    private const string NewPassword = "NewPass42";

    [Fact]
    public async Task Success_clears_the_flag_revokes_every_refresh_token_and_starts_a_new_session()
    {
        var first = await Login("EMP-00009", Password);
        var second = await Login("EMP-00009", Password);
        Clock.Advance(TimeSpan.FromMinutes(1));

        var result = await ChangePassword(TomerId, Password, NewPassword);

        var tomer = UserById(TomerId);
        Assert.False(tomer.MustChangePassword);
        Assert.Equal(FakePasswordHasher.Prefix + NewPassword, tomer.PasswordHash);

        Assert.Equal(Clock.UtcNow, StoredToken(first.RefreshToken.Value).RevokedAt);
        Assert.Equal(Clock.UtcNow, StoredToken(second.RefreshToken.Value).RevokedAt);
        Assert.Null(StoredToken(result.RefreshToken.Value).RevokedAt);
        Assert.Single(RefreshTokens.ActiveFor(TomerId, Clock.UtcNow));

        Assert.False(result.Body.MustChangePassword);
        Assert.False(AccessTokens.Issued[^1].MustChangePassword);

        var entry = AuditLog.Entries[^1];
        Assert.Equal((AuditActions.PasswordChanged, TomerId), (entry.Action, entry.UserId));
    }

    [Fact]
    public async Task Old_password_no_longer_works_and_new_one_does()
    {
        await ChangePassword(NoaId, Password, NewPassword);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Login("EMP-00004", Password));
        await Login("EMP-00004", NewPassword);
    }

    [Fact]
    public async Task Refresh_token_from_before_the_change_is_rejected_but_the_new_session_survives()
    {
        // e.g. an old phone still holding its cookie after the password was changed on the computer
        var oldPhone = await Login("EMP-00004", Password);
        var changed = await ChangePassword(NoaId, Password, NewPassword);
        Clock.Advance(TimeSpan.FromMinutes(5));

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Refresh(oldPhone.RefreshToken.Value));

        Assert.Null(StoredToken(changed.RefreshToken.Value).RevokedAt);
        Assert.DoesNotContain(AuditLog.Entries, e => e.Action == AuditActions.RefreshTokenReuseDetected);
        await Refresh(changed.RefreshToken.Value);
    }

    [Fact]
    public async Task Wrong_current_password_is_400_audited_and_does_not_count_toward_lockout()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() => ChangePassword(NoaId, WrongPassword, NewPassword));

        Assert.Equal("הסיסמה הנוכחית שגויה", exception.Title);
        Assert.Equal(0, Noa.FailedLoginCount);
        Assert.Null(Noa.LockoutEnd);
        Assert.Equal(FakePasswordHasher.Prefix + Password, Noa.PasswordHash);

        var entry = Assert.Single(AuditLog.Entries);
        Assert.Equal((AuditActions.PasswordChangeFailedWrongCurrent, NoaId), (entry.Action, entry.UserId));
    }

    [Fact]
    public async Task Policy_violation_is_400_with_errors_audited_and_nothing_changes()
    {
        var login = await Login("EMP-00009", Password);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => ChangePassword(TomerId, Password, "short"));

        Assert.Contains(PasswordPolicy.TooShort, exception.Errors["newPassword"]);
        Assert.Contains(PasswordPolicy.MissingDigit, exception.Errors["newPassword"]);
        Assert.True(UserById(TomerId).MustChangePassword);
        Assert.Null(StoredToken(login.RefreshToken.Value).RevokedAt);
        Assert.Equal(AuditActions.PasswordChangeFailedPolicy, AuditLog.Entries[^1].Action);
    }

    [Fact]
    public async Task New_password_equal_to_the_current_one_is_rejected()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() => ChangePassword(NoaId, Password, Password));

        Assert.Equal([PasswordPolicy.SameAsCurrent], exception.Errors["newPassword"]);
    }

    [Fact]
    public async Task Inactive_user_cannot_change_password()
    {
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => ChangePassword(OritId, Password, NewPassword));
    }

    [Fact]
    public async Task Log_never_contains_the_passwords()
    {
        await ChangePassword(NoaId, Password, NewPassword);
        await Assert.ThrowsAsync<ValidationException>(() => ChangePassword(NoaId, WrongPassword, "Another77"));

        var log = Logger.AllText;
        foreach (var secret in new[] { Password, NewPassword, WrongPassword, "Another77", FakePasswordHasher.Prefix })
        {
            Assert.DoesNotContain(secret, log);
        }
    }
}
