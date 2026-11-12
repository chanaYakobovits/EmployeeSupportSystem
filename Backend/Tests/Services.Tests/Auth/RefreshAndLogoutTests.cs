using Common.Constants;
using Common.Exceptions;

namespace Services.Tests.Auth;

public class RefreshAndLogoutTests : AuthServiceTestBase
{
    private const string SessionExpired = "session-expired";

    // ---- rotation ----

    [Fact]
    public async Task Refresh_rotates_the_token_and_issues_a_new_access_token()
    {
        var login = await Login("EMP-00004", Password);
        Clock.Advance(TimeSpan.FromMinutes(20));

        var refreshed = await Refresh(login.RefreshToken.Value);

        var old = StoredToken(login.RefreshToken.Value);
        var replacement = StoredToken(refreshed.RefreshToken.Value);
        Assert.NotEqual(login.RefreshToken.Value, refreshed.RefreshToken.Value);
        Assert.Equal(Clock.UtcNow, old.RevokedAt);
        Assert.Same(replacement, old.ReplacedByToken);
        Assert.Null(replacement.RevokedAt);
        Assert.Equal(Clock.UtcNow.AddDays(7), replacement.ExpiresAt);

        Assert.Equal(2, AccessTokens.Issued.Count);
        Assert.Equal(NoaId, refreshed.Body.User.Id);
    }

    [Fact]
    public async Task Refresh_carries_the_current_pending_password_change_flag()
    {
        var login = await Login("EMP-00009", Password);

        var refreshed = await Refresh(login.RefreshToken.Value);

        Assert.True(refreshed.Body.MustChangePassword);
        Assert.True(AccessTokens.Issued[^1].MustChangePassword);
    }

    [Fact]
    public async Task Rotated_chain_keeps_working()
    {
        var token = (await Login("EMP-00004", Password)).RefreshToken.Value;

        for (var i = 0; i < 3; i++)
        {
            Clock.Advance(TimeSpan.FromMinutes(15));
            token = (await Refresh(token)).RefreshToken.Value;
        }

        Assert.Single(RefreshTokens.ActiveFor(NoaId, Clock.UtcNow));
    }

    // ---- reuse detection ----

    [Fact]
    public async Task Reusing_a_rotated_token_after_30_seconds_revokes_all_sessions()
    {
        var login = await Login("EMP-00004", Password);
        var otherDevice = await Login("EMP-00004", Password);
        var refreshed = await Refresh(login.RefreshToken.Value);
        Clock.Advance(TimeSpan.FromSeconds(31));

        var exception = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Refresh(login.RefreshToken.Value));

        Assert.Equal(SessionExpired, exception.ErrorType);
        Assert.Empty(RefreshTokens.ActiveFor(NoaId, Clock.UtcNow));
        Assert.Equal(Clock.UtcNow, StoredToken(refreshed.RefreshToken.Value).RevokedAt);
        Assert.Equal(Clock.UtcNow, StoredToken(otherDevice.RefreshToken.Value).RevokedAt);

        var entry = AuditLog.Entries[^1];
        Assert.Equal(AuditActions.RefreshTokenReuseDetected, entry.Action);
        Assert.Equal((NoaId, 1), (entry.UserId, entry.OrganizationId));

        // The legitimate successor is dead too.
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Refresh(refreshed.RefreshToken.Value));
    }

    [Fact]
    public async Task Reusing_a_rotated_token_within_30_seconds_is_rejected_without_revoking_anything()
    {
        var login = await Login("EMP-00004", Password);
        var refreshed = await Refresh(login.RefreshToken.Value);
        Clock.Advance(TimeSpan.FromSeconds(29));

        var exception = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Refresh(login.RefreshToken.Value));

        Assert.Equal(SessionExpired, exception.ErrorType);
        Assert.Null(StoredToken(refreshed.RefreshToken.Value).RevokedAt);
        Assert.DoesNotContain(AuditLog.Entries, e => e.Action == AuditActions.RefreshTokenReuseDetected);

        // The other tab's new token still works.
        await Refresh(refreshed.RefreshToken.Value);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(3600)]
    public async Task Token_revoked_by_logout_is_rejected_without_revoking_other_sessions(int secondsLater)
    {
        var login = await Login("EMP-00004", Password);
        var otherDevice = await Login("EMP-00004", Password);
        await Logout(login.RefreshToken.Value);
        Clock.Advance(TimeSpan.FromSeconds(secondsLater));

        var exception = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Refresh(login.RefreshToken.Value));

        Assert.Equal(SessionExpired, exception.ErrorType);
        Assert.Null(StoredToken(otherDevice.RefreshToken.Value).RevokedAt);
        Assert.DoesNotContain(AuditLog.Entries, e => e.Action == AuditActions.RefreshTokenReuseDetected);
        await Refresh(otherDevice.RefreshToken.Value); // the other device keeps working
    }

    [Fact]
    public async Task Token_revoked_by_reuse_detection_is_rejected_without_another_reuse_audit()
    {
        var login = await Login("EMP-00004", Password);
        var refreshed = await Refresh(login.RefreshToken.Value);
        Clock.Advance(TimeSpan.FromSeconds(31));
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Refresh(login.RefreshToken.Value)); // reuse
        var reuseAudits = AuditLog.Entries.Count(e => e.Action == AuditActions.RefreshTokenReuseDetected);

        // The successor was revoked by the revoke-all, not by rotation: just rejected.
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Refresh(refreshed.RefreshToken.Value));

        Assert.Equal(1, reuseAudits);
        Assert.Equal(reuseAudits, AuditLog.Entries.Count(e => e.Action == AuditActions.RefreshTokenReuseDetected));
    }

    [Fact]
    public async Task Concurrent_rotation_conflict_is_rejected_and_nothing_is_issued()
    {
        var login = await Login("EMP-00004", Password);
        var stored = StoredToken(login.RefreshToken.Value);
        var tokensBefore = RefreshTokens.Tokens.Count;
        UnitOfWork.ConflictOnNextSave = true;
        // A failed transaction saves nothing: drop the staged replacement and the revocation.
        UnitOfWork.BeforeConflict = () =>
        {
            RefreshTokens.Tokens.RemoveRange(tokensBefore, RefreshTokens.Tokens.Count - tokensBefore);
            stored.RevokedAt = null;
            stored.ReplacedByToken = null;
        };

        var exception = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Refresh(login.RefreshToken.Value));

        Assert.Equal(SessionExpired, exception.ErrorType);
        Assert.Equal(tokensBefore, RefreshTokens.Tokens.Count);
    }

    // ---- other failures ----

    [Fact]
    public async Task Expired_token_is_rejected_without_revoking_other_sessions()
    {
        var login = await Login("EMP-00004", Password);
        Clock.Advance(TimeSpan.FromDays(6));
        var otherDevice = await Login("EMP-00004", Password);
        Clock.Advance(TimeSpan.FromDays(1));

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Refresh(login.RefreshToken.Value));

        Assert.Null(StoredToken(otherDevice.RefreshToken.Value).RevokedAt);
        Assert.DoesNotContain(AuditLog.Entries, e => e.Action == AuditActions.RefreshTokenReuseDetected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-real-token")]
    public async Task Missing_or_unknown_token_is_rejected(string? token)
    {
        await Login("EMP-00004", Password);

        var exception = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Refresh(token));

        Assert.Equal(SessionExpired, exception.ErrorType);
        Assert.Single(RefreshTokens.ActiveFor(NoaId, Clock.UtcNow));
    }

    [Fact]
    public async Task User_who_became_inactive_is_rejected_and_all_tokens_are_revoked()
    {
        var login = await Login("EMP-00004", Password);
        await Login("EMP-00004", Password);
        Noa.IsActive = false;

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Refresh(login.RefreshToken.Value));

        Assert.Empty(RefreshTokens.ActiveFor(NoaId, Clock.UtcNow));
        Assert.Equal(AuditActions.RefreshFailedInactive, AuditLog.Entries[^1].Action);
    }

    // ---- logout ----

    [Fact]
    public async Task Logout_revokes_only_the_current_token()
    {
        var login = await Login("EMP-00004", Password);
        var otherDevice = await Login("EMP-00004", Password);

        await Logout(login.RefreshToken.Value);

        Assert.Equal(Start, StoredToken(login.RefreshToken.Value).RevokedAt);
        Assert.Null(StoredToken(otherDevice.RefreshToken.Value).RevokedAt);
        var entry = AuditLog.Entries[^1];
        Assert.Equal((AuditActions.Logout, NoaId), (entry.Action, entry.UserId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-real-token")]
    public async Task Logout_with_missing_or_unknown_token_does_not_throw(string? token)
    {
        await Logout(token);

        Assert.Empty(AuditLog.Entries);
    }

    [Fact]
    public async Task Second_logout_with_the_same_token_does_nothing()
    {
        var login = await Login("EMP-00004", Password);
        await Logout(login.RefreshToken.Value);
        var auditCount = AuditLog.Entries.Count;

        await Logout(login.RefreshToken.Value);

        Assert.Equal(auditCount, AuditLog.Entries.Count);
    }

    // ---- logging ----

    [Fact]
    public async Task Log_never_contains_refresh_token_values_or_hashes()
    {
        var login = await Login("EMP-00004", Password);
        var refreshed = await Refresh(login.RefreshToken.Value);
        Clock.Advance(TimeSpan.FromMinutes(1));
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Refresh(login.RefreshToken.Value));
        await Logout(refreshed.RefreshToken.Value);

        var log = Logger.AllText;
        foreach (var value in new[] { login.RefreshToken.Value, refreshed.RefreshToken.Value })
        {
            Assert.DoesNotContain(value, log);
            Assert.DoesNotContain(Services.Implementations.SecureTokenGenerator.Hash(value), log);
        }
    }
}
