using Common.Constants;
using Common.Enums;
using Services.Implementations;
using Services.Interfaces;

namespace Services.Tests.Invitations;

/// <summary>The background sender: token only at send time, only its hash stored, retries and final failure.</summary>
public sealed class InvitationEmailProcessorTests : InvitationTestBase
{
    [Fact]
    public async Task Sending_creates_the_token_stores_only_its_hash_and_marks_the_job_succeeded()
    {
        var (userId, token) = await InviteAndSendAsync();

        var job = Assert.Single(Invitations.Jobs);
        var invitation = Assert.Single(Invitations.Invitations);
        Assert.Equal(InvitationJobStatus.Succeeded, job.Status);
        Assert.Same(invitation, job.Invitation);
        Assert.Equal(userId, invitation.UserId);
        Assert.Equal(SecureTokenGenerator.Hash(token), invitation.TokenHash);
        Assert.NotEqual(token, invitation.TokenHash);
        Assert.Equal("new.employee@example.com", Assert.Single(EmailSender.Sent).ToAddress);

        var audit = AuditLog.Entries.Single(a => a.Action == AuditActions.InvitationSent);
        Assert.Null(audit.UserId); // system action
        Assert.Equal(userId, audit.EntityId);
        Assert.Equal(InvitationStatus.Sent, (await GetUser(userId)).Invitation?.Status);
    }

    [Fact]
    public async Task The_link_is_valid_for_72_hours_from_sending()
    {
        await InviteNewEmployeeAsync();
        Clock.Advance(TimeSpan.FromMinutes(3)); // sent a bit after creation

        await RunWorkerAsync();

        Assert.Equal(Now.AddMinutes(3).AddHours(72), Assert.Single(Invitations.Invitations).ExpiresAt);
    }

    [Fact]
    public async Task The_raw_token_appears_nowhere_but_in_the_email()
    {
        var (_, token) = await InviteAndSendAsync();

        var job = Assert.Single(Invitations.Jobs);
        var jobValues = typeof(Common.Models.InvitationEmailJob).GetProperties()
            .Select(p => p.GetValue(job)?.ToString() ?? "");
        Assert.DoesNotContain(jobValues, value => value.Contains(token));
        Assert.DoesNotContain(token, ProcessorLogger.AllText);
        Assert.DoesNotContain(token, UserLogger.AllText);
        Assert.DoesNotContain(AuditLog.Entries, a => $"{a.Action}{a.EntityType}{a.IpAddress}".Contains(token));
        Assert.DoesNotContain("new.employee@example.com", ProcessorLogger.AllText);
        Assert.Contains(token, EmailSender.Sent[0].HtmlBody);
    }

    [Fact]
    public async Task A_transient_failure_schedules_a_retry_and_revokes_the_unsent_link()
    {
        await InviteNewEmployeeAsync();
        EmailSender.FailWith = new EmailSendException(EmailErrorCode.SmtpConnectionFailed, isTransient: true, "SocketException");

        await RunWorkerAsync();

        var job = Assert.Single(Invitations.Jobs);
        Assert.Equal(InvitationJobStatus.Pending, job.Status);
        Assert.Equal(1, job.Attempts);
        Assert.Equal(Now.AddMinutes(1), job.NextAttemptAt);
        Assert.Equal(nameof(EmailErrorCode.SmtpConnectionFailed), job.LastErrorCode);
        Assert.NotNull(Assert.Single(Invitations.Invitations).RevokedAt);
        Assert.DoesNotContain(AuditLog.Entries, a => a.Action == AuditActions.InvitationSendFailed);
    }

    [Fact]
    public async Task A_retry_that_succeeds_sends_a_new_token_and_leaves_one_valid_link()
    {
        var userId = await InviteNewEmployeeAsync();
        EmailSender.FailWith = new EmailSendException(EmailErrorCode.Timeout, isTransient: true, "TimeoutException");
        await RunWorkerAsync();
        EmailSender.FailWith = null;

        await RunWorkerAsync(); // not due yet
        Assert.Empty(EmailSender.Sent);

        Clock.Advance(TimeSpan.FromMinutes(1));
        await RunWorkerAsync();

        Assert.Equal(InvitationJobStatus.Succeeded, Assert.Single(Invitations.Jobs).Status);
        Assert.Equal(2, Invitations.Invitations.Count);
        var valid = Assert.Single(Invitations.ValidFor(userId, Clock.UtcNow));
        Assert.Equal(SecureTokenGenerator.Hash(EmailSender.LastToken()), valid.TokenHash);
    }

    [Fact]
    public async Task After_the_last_attempt_the_job_is_failed_with_the_error_code_and_audited()
    {
        var userId = await InviteNewEmployeeAsync();
        EmailSender.FailWith = new EmailSendException(EmailErrorCode.SmtpConnectionFailed, isTransient: true, "SocketException");

        for (var attempt = 1; attempt <= Options.MaxAttempts; attempt++)
        {
            await RunWorkerAsync();
            Clock.Advance(TimeSpan.FromHours(1));
        }

        var job = Assert.Single(Invitations.Jobs);
        Assert.Equal(InvitationJobStatus.Failed, job.Status);
        Assert.Equal(Options.MaxAttempts, job.Attempts);
        Assert.Equal(nameof(EmailErrorCode.SmtpConnectionFailed), job.LastErrorCode);
        Assert.Empty(Invitations.ValidFor(userId, Clock.UtcNow));
        var audit = Assert.Single(AuditLog.Entries, a => a.Action == AuditActions.InvitationSendFailed);
        Assert.Equal(userId, audit.EntityId);
        Assert.Equal(InvitationStatus.Failed, (await GetUser(userId)).Invitation?.Status);
    }

    [Fact]
    public async Task A_permanent_failure_fails_the_job_at_once()
    {
        await InviteNewEmployeeAsync();
        EmailSender.FailWith = new EmailSendException(EmailErrorCode.RecipientRejected, isTransient: false, "SmtpCommandException");

        await RunWorkerAsync();

        var job = Assert.Single(Invitations.Jobs);
        Assert.Equal(InvitationJobStatus.Failed, job.Status);
        Assert.Equal(1, job.Attempts);
        Assert.Equal(nameof(EmailErrorCode.RecipientRejected), job.LastErrorCode);
    }

    [Fact]
    public async Task A_job_cancelled_while_processing_sends_nothing()
    {
        await InviteNewEmployeeAsync();
        // A resend cancelled the job between the claim and the save: the save hits the Status concurrency token.
        UnitOfWork.ConflictOnNextSave = true;

        await RunWorkerAsync();

        Assert.Empty(EmailSender.Sent);
    }

    [Fact]
    public async Task A_job_for_a_user_who_already_set_a_password_is_cancelled_without_sending()
    {
        var userId = await InviteNewEmployeeAsync();
        UserById(userId).MustChangePassword = false;

        await RunWorkerAsync();

        var job = Assert.Single(Invitations.Jobs);
        Assert.Equal(InvitationJobStatus.Cancelled, job.Status);
        Assert.Equal(InvitationCancelReasons.PasswordAlreadySet, job.LastErrorCode);
        Assert.Empty(EmailSender.Sent);
        Assert.Empty(Invitations.Invitations);
    }

    [Fact]
    public async Task A_job_for_an_inactive_user_is_cancelled_without_sending()
    {
        var userId = await InviteNewEmployeeAsync();
        UserById(userId).IsActive = false;

        await RunWorkerAsync();

        Assert.Equal(InvitationCancelReasons.UserInactive, Assert.Single(Invitations.Jobs).LastErrorCode);
        Assert.Empty(EmailSender.Sent);
    }

    [Fact]
    public async Task A_job_whose_attempts_crashed_too_often_is_failed_instead_of_claimed_forever()
    {
        await InviteNewEmployeeAsync();
        // Every round crashed after the claim (no result recorded): the lock expires and the job is claimed again.
        for (var round = 0; round < Options.MaxAttempts; round++)
        {
            await Processor.ClaimDueAsync(CancellationToken.None);
            Clock.Advance(TimeSpan.FromMinutes(Options.LockMinutes + 1));
        }

        await RunWorkerAsync();

        var job = Assert.Single(Invitations.Jobs);
        Assert.Equal(InvitationJobStatus.Failed, job.Status);
        Assert.Empty(EmailSender.Sent);
    }
}
