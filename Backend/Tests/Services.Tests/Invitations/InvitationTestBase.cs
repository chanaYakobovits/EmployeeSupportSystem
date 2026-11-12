using Common.DTOs.Auth;
using Common.DTOs.Users;
using Common.Options;
using Microsoft.Extensions.Options;
using Services.Implementations.Invitations;
using Services.Tests.Admin;
using Services.Tests.Fakes;
using Services.Tests.Invitations.Fakes;
using Services.Validators;

namespace Services.Tests.Invitations;

/// <summary>
/// The admin organization of <see cref="AdminTestBase"/> + the invitation pieces: the worker's processor with a
/// capturing e-mail sender, and the anonymous set-password service.
/// </summary>
public abstract class InvitationTestBase : AdminTestBase
{
    protected const string NewPassword = "NewPass123";
    protected const string FrontendBaseUrl = "https://ovdenet.example.com";

    protected readonly InvitationOptions Options = new() { FrontendBaseUrl = FrontendBaseUrl };
    protected readonly CapturingEmailSender EmailSender = new();
    protected readonly InvitationEmailComposer Composer;
    protected readonly CapturingLogger<InvitationEmailProcessor> ProcessorLogger = new();
    protected readonly CapturingLogger<InvitationService> InvitationLogger = new();
    protected readonly InvitationEmailProcessor Processor;
    protected readonly InvitationService InvitationService;

    protected InvitationTestBase()
    {
        var options = Microsoft.Extensions.Options.Options.Create(Options);
        Composer = new InvitationEmailComposer(options);
        Processor = new InvitationEmailProcessor(Invitations, Users, AuditLog, UnitOfWork, EmailSender, Composer, Clock,
            options, ProcessorLogger);
        InvitationService = new InvitationService(Invitations, RefreshTokens, AuditLog, UnitOfWork, Hasher, Clock,
            new SetPasswordRequestValidator(), InvitationLogger);
    }

    /// <summary>Creates an invited employee (no password, job queued) and returns their id.</summary>
    protected async Task<int> InviteNewEmployeeAsync(string email = "new.employee@example.com")
    {
        var user = await UserService.CreateAsync(
            new CreateUserRequest("נגה", "ברק", email, DevelopmentId, "רכזת", null), Ip, CancellationToken.None);
        return user.Id;
    }

    /// <summary>One worker round: claim the due jobs and process each.</summary>
    protected async Task RunWorkerAsync()
    {
        foreach (var job in await Processor.ClaimDueAsync(CancellationToken.None))
        {
            await Processor.ProcessAsync(job.JobId, CancellationToken.None);
        }
    }

    /// <summary>Invites a new employee and sends the e-mail; returns the user id and the token from the link.</summary>
    protected async Task<(int UserId, string Token)> InviteAndSendAsync(string email = "new.employee@example.com")
    {
        var userId = await InviteNewEmployeeAsync(email);
        await RunWorkerAsync();
        return (userId, EmailSender.LastToken());
    }

    protected Task SetPassword(string token, string password = NewPassword) =>
        InvitationService.SetPasswordAsync(new SetPasswordRequest(token, password), Ip, CancellationToken.None);

    protected Task<UserDetailsDto> GetUser(int userId) => UserService.GetAsync(userId, CancellationToken.None);
}
