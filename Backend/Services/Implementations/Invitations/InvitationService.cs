using Common.Constants;
using Common.DTOs.Auth;
using Common.Exceptions;
using Common.Interfaces;
using Common.Models;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Repositories.Interfaces;
using Services.Interfaces;
using Services.Validators;
using ValidationException = Common.Exceptions.ValidationException;

namespace Services.Implementations.Invitations;

/// <summary>
/// POST /api/auth/set-password. Anonymous: the token is the proof. Every invalid link – unknown, used, revoked,
/// expired, or its user inactive – gets the same response, so the endpoint reveals nothing about tokens or users.
/// Never logs or audits the token (only the reason code and the user id when known).
/// </summary>
public sealed class InvitationService(
    IUserInvitationRepository invitations,
    IRefreshTokenRepository refreshTokens,
    IAuditLogRepository auditLogs,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IDateTimeProvider clock,
    IValidator<SetPasswordRequest> validator,
    ILogger<InvitationService> logger) : IInvitationService
{
    public const string InvalidLinkMessage = "הקישור אינו בתוקף. יש לפנות למנהל המערכת.";
    public const string InvalidLinkErrorType = "invalid-invitation";

    public async Task SetPasswordAsync(SetPasswordRequest request, string ipAddress, CancellationToken cancellationToken)
    {
        await validator.EnsureValidAsync(request, cancellationToken);

        var now = clock.UtcNow;
        var invitation = await invitations.FindByTokenHashAsync(SecureTokenGenerator.Hash(request.Token.Trim()),
            cancellationToken);

        var invalidReason = invitation is null ? "Unknown"
            : invitation.UsedAt is not null ? "Used"
            : invitation.RevokedAt is not null ? "Revoked"
            : invitation.ExpiresAt <= now ? "Expired"
            : !invitation.User.IsActive ? "UserInactive"
            : null;
        if (invalidReason is not null)
        {
            await RejectAsync(invitation?.User, invalidReason, ipAddress, now, cancellationToken);
        }

        var user = invitation!.User;
        var policyErrors = PasswordPolicy.Validate(request.NewPassword);
        if (policyErrors.Count > 0)
        {
            // The link stays valid: the employee fixes the password and submits again.
            throw new ValidationException("הסיסמה אינה עומדת בדרישות",
                new Dictionary<string, string[]> { ["newPassword"] = policyErrors.ToArray() }, errorType: "password-policy");
        }

        var passwordSet = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // The conditional UPDATE decides: of two clicks at the same moment only one marks the link used.
            if (!await invitations.TryMarkUsedAsync(invitation.Id, now, ct))
            {
                return false;
            }

            user.PasswordHash = passwordHasher.Hash(request.NewPassword);
            user.MustChangePassword = false;
            user.FailedLoginCount = 0;
            user.LockoutEnd = null;

            // Other links and queued e-mails of this user are now pointless; a temporary password may have sessions.
            await InvitationQueue.CancelOpenAsync(invitations, user.Id, now, ct);
            foreach (var token in await refreshTokens.GetActiveForUserAsync(user.Id, now, ct))
            {
                token.RevokedAt = now;
            }

            auditLogs.Add(AuditEntries.ForUser(AuditActions.InvitationPasswordSet, user.OrganizationId, user.Id, user.Id,
                ipAddress, now));
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        if (!passwordSet)
        {
            await RejectAsync(user, "UsedConcurrently", ipAddress, now, cancellationToken);
        }

        logger.LogInformation("Password set with an invitation link by user {UserId}", user.Id);
    }

    /// <summary>Audits (when the user is known), logs the reason code and throws the one uniform error.</summary>
    private async Task RejectAsync(User? user, string reason, string ipAddress, DateTime now,
        CancellationToken cancellationToken)
    {
        if (user is not null)
        {
            // No actor: whoever holds a dead link is not necessarily the user.
            auditLogs.Add(new AuditLog
            {
                OrganizationId = user.OrganizationId,
                UserId = null,
                Action = AuditActions.InvitationPasswordSetFailed,
                EntityType = AuditEntityTypes.User,
                EntityId = user.Id,
                IpAddress = ipAddress,
                CreatedAt = now,
            });
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        logger.LogWarning("Set-password rejected: {Reason}, user {UserId}", reason, user?.Id);
        throw InvalidLink();
    }

    public static ValidationException InvalidLink() =>
        new(InvalidLinkMessage, errorType: InvalidLinkErrorType);
}
