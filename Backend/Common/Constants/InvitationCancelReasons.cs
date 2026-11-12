namespace Common.Constants;

/// <summary>
/// <c>InvitationEmailJob.LastErrorCode</c> of a job the worker cancelled instead of sending
/// (next to the <see cref="Enums.EmailErrorCode"/> values of failed sends).
/// </summary>
public static class InvitationCancelReasons
{
    public const string UserInactive = "UserInactive";
    public const string PasswordAlreadySet = "PasswordAlreadySet";
    public const string UserNotFound = "UserNotFound";
}
