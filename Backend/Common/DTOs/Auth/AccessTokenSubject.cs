using Common.Enums;

namespace Common.DTOs.Auth;

/// <summary>What goes into the access token claims. Contains no names, e-mail or other personal details.</summary>
public sealed record AccessTokenSubject(
    int UserId,
    int OrganizationId,
    int DepartmentId,
    IReadOnlyCollection<Role> Roles,
    bool MustChangePassword);

public sealed record AccessToken(string Token, DateTime ExpiresAt);
