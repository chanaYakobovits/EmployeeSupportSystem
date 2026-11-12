namespace Common.Models;

public class User
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public int DepartmentId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    /// <summary>
    /// Null for an invited user who has not set a password yet: no password can sign in (AuthService treats it as a
    /// wrong password).
    /// </summary>
    public string? PasswordHash { get; set; }

    /// <summary>
    /// True until the user chose a password of their own (invited, or a temporary password from the admin).
    /// An invitation can be (re)sent only while this is true.
    /// </summary>
    public bool MustChangePassword { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public Organization Organization { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public ICollection<UserRole> Roles { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
