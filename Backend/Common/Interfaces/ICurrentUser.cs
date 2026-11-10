using Common.Enums;

namespace Common.Interfaces;

/// <summary>
/// The authenticated user of the current request, read from the access token claims.
/// The only way the Services layer learns who the user is – never from a request body (spec 6.2).
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Throws <see cref="InvalidOperationException"/> when no user is authenticated.</summary>
    int UserId { get; }

    /// <summary>Throws <see cref="InvalidOperationException"/> when no user is authenticated.</summary>
    int OrganizationId { get; }

    /// <summary>Throws <see cref="InvalidOperationException"/> when no user is authenticated.</summary>
    int DepartmentId { get; }

    /// <summary>Empty when no user is authenticated.</summary>
    IReadOnlyCollection<Role> Roles { get; }

    bool MustChangePassword { get; }

    bool IsInRole(Role role);
}
