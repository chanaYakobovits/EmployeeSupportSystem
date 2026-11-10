using Common.DTOs.Auth;
using Common.Models;

namespace Repositories.Interfaces;

public interface IUserRepository
{
    /// <summary>Tracked user with Roles and Department, for the login flow (which updates the lockout counters). Null if not found.</summary>
    Task<User?> FindForLoginByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>Tracked user with Roles and Department, for the login flow. Null if not found.</summary>
    Task<User?> FindForLoginByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken);

    /// <summary>Read-only projection of an active user. Null if not found or inactive.</summary>
    Task<CurrentUserDto?> GetCurrentUserAsync(int userId, CancellationToken cancellationToken);
}
