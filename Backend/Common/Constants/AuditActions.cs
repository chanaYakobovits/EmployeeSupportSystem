namespace Common.Constants;

/// <summary>Values of <c>AuditLog.Action</c> (spec 6.4). Max length 50.</summary>
public static class AuditActions
{
    public const string LoginSucceeded = "LoginSucceeded";
    public const string LoginFailedWrongPassword = "LoginFailed.WrongPassword";
    public const string LoginFailedInactive = "LoginFailed.Inactive";
    public const string LoginFailedUnknownUser = "LoginFailed.UnknownUser";
    public const string LoginBlockedLockedOut = "LoginBlocked.LockedOut";
}

/// <summary>Values of <c>AuditLog.EntityType</c>.</summary>
public static class AuditEntityTypes
{
    public const string User = "User";
}
