namespace Common.Enums;

/// <summary>Why sending an e-mail failed. Only this code is stored and logged – never the message or the address.</summary>
public enum EmailErrorCode
{
    SmtpConnectionFailed,
    SmtpAuthenticationFailed,
    RecipientRejected,
    Timeout,
    FileWriteFailed,
    Unknown,
}
