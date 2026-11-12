using Common.Constants;
using Common.Enums;
using Common.Exceptions;
using Services.Implementations.Invitations;
using Services.Tests.Auth.Fakes;

namespace Services.Tests.Invitations;

/// <summary>POST /api/auth/set-password: one-time link, expiry, uniform error, policy.</summary>
public sealed class SetPasswordTests : InvitationTestBase
{
    [Fact]
    public async Task A_valid_link_sets_the_password_and_is_marked_used()
    {
        var (userId, token) = await InviteAndSendAsync();

        await SetPassword(token);

        var user = UserById(userId);
        Assert.Equal(FakePasswordHasher.Prefix + NewPassword, user.PasswordHash);
        Assert.False(user.MustChangePassword);
        Assert.Equal(Now, Assert.Single(Invitations.Invitations).UsedAt);
        var audit = Assert.Single(AuditLog.Entries, a => a.Action == AuditActions.InvitationPasswordSet);
        Assert.Equal(userId, audit.UserId);
        Assert.Equal(userId, audit.EntityId);
        Assert.Equal(Ip, audit.IpAddress);
        Assert.Equal(InvitationStatus.Used, (await GetUser(userId)).Invitation?.Status);
    }

    [Fact]
    public async Task The_same_link_works_only_once()
    {
        var (userId, token) = await InviteAndSendAsync();
        await SetPassword(token);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => SetPassword(token, "Another123"));

        AssertUniform(exception);
        Assert.Equal(FakePasswordHasher.Prefix + NewPassword, UserById(userId).PasswordHash);
    }

    [Fact]
    public async Task A_link_used_concurrently_by_another_request_is_rejected()
    {
        var (userId, token) = await InviteAndSendAsync();
        // Two clicks at once: both read a valid link, the other request's conditional UPDATE wins.
        Invitations.LoseNextMarkUsed = true;

        var exception = await Assert.ThrowsAsync<ValidationException>(() => SetPassword(token));

        AssertUniform(exception);
        Assert.Null(UserById(userId).PasswordHash);
        Assert.True(UserById(userId).MustChangePassword);
    }

    [Fact]
    public async Task The_link_expires_after_72_hours()
    {
        var (userId, token) = await InviteAndSendAsync();
        Clock.Advance(TimeSpan.FromHours(72));

        var exception = await Assert.ThrowsAsync<ValidationException>(() => SetPassword(token));

        AssertUniform(exception);
        Assert.Null(UserById(userId).PasswordHash);
        Assert.Equal(InvitationStatus.Expired, (await GetUser(userId)).Invitation?.Status);
    }

    [Fact]
    public async Task The_link_still_works_just_before_it_expires()
    {
        var (userId, token) = await InviteAndSendAsync();
        Clock.Advance(TimeSpan.FromHours(72) - TimeSpan.FromSeconds(1));

        await SetPassword(token);

        Assert.False(UserById(userId).MustChangePassword);
    }

    [Fact]
    public async Task Unknown_used_expired_revoked_and_inactive_user_all_get_the_same_answer()
    {
        // Expired: sent 73 hours ago.
        var (_, expired) = await InviteAndSendAsync("expired@example.com");
        Clock.Advance(TimeSpan.FromHours(73));

        // The others are fresh links, still in time.
        var (_, used) = await InviteAndSendAsync("used@example.com");
        await SetPassword(used);
        var (revokedUserId, revoked) = await InviteAndSendAsync("revoked@example.com");
        await UserService.ResetPasswordAsync(revokedUserId, Ip, CancellationToken.None);
        var (inactiveUserId, inactive) = await InviteAndSendAsync("inactive@example.com");
        UserById(inactiveUserId).IsActive = false;

        var answers = new List<ValidationException>();
        foreach (var token in new[] { "no-such-token", used, expired, revoked, inactive })
        {
            answers.Add(await Assert.ThrowsAsync<ValidationException>(() => SetPassword(token)));
        }

        Assert.All(answers, AssertUniform);
        Assert.Null(UserById(inactiveUserId).PasswordHash);
    }

    [Fact]
    public async Task A_missing_or_oversized_token_gets_the_same_message()
    {
        var missing = await Assert.ThrowsAsync<ValidationException>(() => SetPassword(""));
        var oversized = await Assert.ThrowsAsync<ValidationException>(() => SetPassword(new string('a', 500)));

        Assert.Equal([InvitationService.InvalidLinkMessage], missing.Errors["token"]);
        Assert.Equal([InvitationService.InvalidLinkMessage], oversized.Errors["token"]);
    }

    [Fact]
    public async Task A_weak_password_is_rejected_and_the_link_stays_valid()
    {
        var (userId, token) = await InviteAndSendAsync();

        var exception = await Assert.ThrowsAsync<ValidationException>(() => SetPassword(token, "short"));

        Assert.Equal("password-policy", exception.ErrorType);
        Assert.True(exception.Errors.ContainsKey("newPassword"));
        Assert.Null(Assert.Single(Invitations.Invitations).UsedAt);

        await SetPassword(token); // fixed and submitted again
        Assert.False(UserById(userId).MustChangePassword);
    }

    [Fact]
    public async Task Setting_the_password_revokes_other_links_cancels_queued_emails_and_signs_out_everywhere()
    {
        var (userId, token) = await InviteAndSendAsync();
        // A newer job is queued (e.g. a resend that the worker has not sent yet) and a session exists.
        Invitations.AddJob(InvitationQueue.NewJob(UserById(userId), Now));
        var otherInvitation = new Common.Models.UserInvitation
        {
            UserId = userId, TokenHash = "OTHER", CreatedAt = Now, ExpiresAt = Now.AddHours(72),
        };
        Invitations.AddInvitation(otherInvitation);
        AddActiveRefreshTokens(userId, 1);

        await SetPassword(token);

        Assert.NotNull(otherInvitation.RevokedAt);
        Assert.Equal(InvitationJobStatus.Cancelled, Invitations.Jobs[^1].Status);
        Assert.Empty(RefreshTokens.ActiveFor(userId, Now));
    }

    [Fact]
    public async Task A_rejected_link_is_audited_without_an_actor_and_never_logs_the_token()
    {
        var (userId, token) = await InviteAndSendAsync();
        await SetPassword(token);

        await Assert.ThrowsAsync<ValidationException>(() => SetPassword(token));

        var audit = Assert.Single(AuditLog.Entries, a => a.Action == AuditActions.InvitationPasswordSetFailed);
        Assert.Null(audit.UserId);
        Assert.Equal(userId, audit.EntityId);
        Assert.DoesNotContain(token, InvitationLogger.AllText);
        Assert.DoesNotContain(NewPassword, InvitationLogger.AllText);
    }

    private static void AssertUniform(ValidationException exception)
    {
        Assert.Equal(InvitationService.InvalidLinkMessage, exception.Title);
        Assert.Equal(InvitationService.InvalidLinkErrorType, exception.ErrorType);
        Assert.Empty(exception.Errors);
    }
}
