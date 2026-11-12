namespace Common.Constants;

/// <summary>Values of <c>AuditLog.Action</c> (spec 6.4). Max length 50.</summary>
public static class AuditActions
{
    public const string LoginSucceeded = "LoginSucceeded";
    public const string LoginFailedWrongPassword = "LoginFailed.WrongPassword";
    public const string LoginFailedInactive = "LoginFailed.Inactive";
    public const string LoginFailedUnknownUser = "LoginFailed.UnknownUser";
    public const string LoginBlockedLockedOut = "LoginBlocked.LockedOut";
    public const string Logout = "Logout";
    public const string RefreshTokenReuseDetected = "RefreshToken.ReuseDetected";
    public const string RefreshFailedInactive = "RefreshFailed.Inactive";
    public const string PasswordChanged = "PasswordChanged";
    public const string PasswordChangeFailedWrongCurrent = "PasswordChangeFailed.WrongCurrent";
    public const string PasswordChangeFailedPolicy = "PasswordChangeFailed.Policy";

    // User administration (task 2.3). Never any password, only actor / target / IP.
    public const string UserCreated = "User.Created";
    public const string UserUpdated = "User.Updated";
    public const string UserRolesChanged = "User.RolesChanged";
    public const string UserDeactivated = "User.Deactivated";
    public const string UserActivated = "User.Activated";
    public const string UserPasswordReset = "User.PasswordReset";

    // Categories and SLA (task 2.4). Only actor / entity / IP – never the description text.
    public const string CategoryCreated = "Category.Created";
    public const string CategoryUpdated = "Category.Updated";
    public const string CategoryDeactivated = "Category.Deactivated";
    public const string CategoryActivated = "Category.Activated";
    public const string SlaPoliciesUpdated = "SlaPolicies.Updated";

    // Invitation e-mails. Never the token or the e-mail address. Sent / SendFailed are system actions (UserId null).
    public const string InvitationSent = "Invitation.Sent";
    public const string InvitationSendFailed = "Invitation.SendFailed";
    public const string InvitationResent = "Invitation.Resent";
    public const string InvitationPasswordSet = "Invitation.PasswordSet";
    public const string InvitationPasswordSetFailed = "Invitation.PasswordSetFailed";
    public const string LoginFailedPasswordNotSet = "LoginFailed.PasswordNotSet";
}

/// <summary>Values of <c>AuditLog.EntityType</c>.</summary>
public static class AuditEntityTypes
{
    public const string User = "User";
    public const string Category = "Category";

    /// <summary>The organization's SLA policies as a whole (EntityId null).</summary>
    public const string SlaPolicy = "SlaPolicy";
}
