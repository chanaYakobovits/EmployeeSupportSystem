using Common.Constants;
using Common.Enums;
using Common.Exceptions;

namespace Services.Tests.Invitations;

/// <summary>POST /api/users/{id}/invitation and the effect of reset-password on invitations.</summary>
public sealed class ResendAndResetTests : InvitationTestBase
{
    private Task<Common.DTOs.Users.UserDetailsDto> Resend(int userId) =>
        UserService.ResendInvitationAsync(userId, Ip, CancellationToken.None);

    [Fact]
    public async Task Resend_revokes_the_previous_link_and_queues_a_new_email()
    {
        var (userId, oldToken) = await InviteAndSendAsync();
        Clock.Advance(TimeSpan.FromMinutes(10));

        var response = await Resend(userId);

        Assert.NotNull(Assert.Single(Invitations.Invitations).RevokedAt);
        Assert.Equal(InvitationJobStatus.Pending, Invitations.Jobs[^1].Status);
        Assert.Equal(InvitationStatus.Pending, response.Invitation?.Status);
        var audit = Assert.Single(AuditLog.Entries, a => a.Action == AuditActions.InvitationResent);
        Assert.Equal(YaelId, audit.UserId);
        Assert.Equal(userId, audit.EntityId);

        await RunWorkerAsync();
        var newToken = EmailSender.LastToken();
        Assert.NotEqual(oldToken, newToken);
        await Assert.ThrowsAsync<ValidationException>(() => SetPassword(oldToken));
        await SetPassword(newToken);
        Assert.False(UserById(userId).MustChangePassword);
    }

    [Fact]
    public async Task Resend_before_the_first_email_went_out_cancels_it_so_only_one_email_is_sent()
    {
        var userId = await InviteNewEmployeeAsync();

        await Resend(userId);
        await RunWorkerAsync();

        Assert.Equal([InvitationJobStatus.Cancelled, InvitationJobStatus.Succeeded], Invitations.Jobs.Select(j => j.Status));
        Assert.Single(EmailSender.Sent);
    }

    [Fact]
    public async Task Resend_to_a_user_who_already_set_a_password_is_refused()
    {
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => Resend(NoaId));

        Assert.Equal("password-already-set", exception.ErrorType);
        Assert.Empty(Invitations.Jobs);
    }

    [Fact]
    public async Task Resend_to_an_inactive_user_is_refused()
    {
        var userId = await InviteNewEmployeeAsync();
        UserById(userId).IsActive = false;

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => Resend(userId));

        Assert.Equal("inactive-user", exception.ErrorType);
    }

    [Fact]
    public async Task Resend_to_an_unknown_user_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Resend(999));
    }

    [Fact]
    public async Task Resend_after_a_failed_send_works()
    {
        var userId = await InviteNewEmployeeAsync();
        EmailSender.FailWith = new Services.Interfaces.EmailSendException(EmailErrorCode.RecipientRejected, false, "X");
        await RunWorkerAsync();
        EmailSender.FailWith = null;

        await Resend(userId);
        await RunWorkerAsync();

        Assert.Equal(InvitationStatus.Sent, (await GetUser(userId)).Invitation?.Status);
    }

    [Fact]
    public async Task Reset_password_revokes_the_open_link_and_shows_Revoked()
    {
        var (userId, token) = await InviteAndSendAsync();

        var reset = await UserService.ResetPasswordAsync(userId, Ip, CancellationToken.None);

        Assert.NotNull(Assert.Single(Invitations.Invitations).RevokedAt);
        Assert.Equal(InvitationStatus.Revoked, (await GetUser(userId)).Invitation?.Status);
        await Assert.ThrowsAsync<ValidationException>(() => SetPassword(token));
        Assert.True(Hasher.Verify(UserById(userId).PasswordHash!, reset.TemporaryPassword));
    }

    [Fact]
    public async Task Reset_password_cancels_an_email_that_was_not_sent_yet()
    {
        var userId = await InviteNewEmployeeAsync();

        await UserService.ResetPasswordAsync(userId, Ip, CancellationToken.None);
        await RunWorkerAsync();

        Assert.Equal(InvitationJobStatus.Cancelled, Assert.Single(Invitations.Jobs).Status);
        Assert.Empty(EmailSender.Sent);
        Assert.Null((await GetUser(userId)).Invitation); // never sent → no status
    }

    [Fact]
    public async Task After_a_reset_the_admin_can_still_send_an_invitation_instead()
    {
        var userId = await InviteNewEmployeeAsync();
        await UserService.ResetPasswordAsync(userId, Ip, CancellationToken.None);

        await Resend(userId); // MustChangePassword is still true: the temporary password is not the user's own
        await RunWorkerAsync();

        Assert.Equal(InvitationStatus.Sent, (await GetUser(userId)).Invitation?.Status);
    }

    [Fact]
    public async Task A_user_who_was_never_invited_has_no_invitation_status()
    {
        Assert.Null((await GetUser(NoaId)).Invitation);
    }
}
