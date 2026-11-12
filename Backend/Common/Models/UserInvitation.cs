namespace Common.Models;

/// <summary>
/// A one-time link for setting the first password (invitation e-mail). Created by the background sender at the moment
/// of sending; only the SHA-256 hash of the token is stored – the token itself exists only in the e-mail.
/// Valid while UsedAt and RevokedAt are null and ExpiresAt is in the future.
/// </summary>
public class UserInvitation
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
