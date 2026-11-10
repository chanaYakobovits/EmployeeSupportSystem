using Common.Enums;

namespace Common.Models;

public class UserRole
{
    public int UserId { get; set; }
    public Role Role { get; set; }

    public User User { get; set; } = null!;
}
