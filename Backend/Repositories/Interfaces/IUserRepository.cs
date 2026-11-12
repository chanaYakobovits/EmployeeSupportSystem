using Common.DTOs.Auth;
using Common.DTOs.Paging;
using Common.DTOs.Users;
using Common.Models;

namespace Repositories.Interfaces;

public interface IUserRepository
{
    /// <summary>Tracked user with Roles and Department, for the login flow (which updates the lockout counters). Null if not found.</summary>
    Task<User?> FindForLoginByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>Tracked user with Roles and Department, for the login flow. Null if not found.</summary>
    Task<User?> FindForLoginByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken);

    /// <summary>Tracked user with Roles and Department, within the signed-in user's organization. Null if not found.</summary>
    Task<User?> FindForUpdateAsync(int userId, CancellationToken cancellationToken);

    /// <summary>Read-only projection of an active user. Null if not found or inactive.</summary>
    Task<CurrentUserDto?> GetCurrentUserAsync(int userId, CancellationToken cancellationToken);

    // ---- user administration (task 2.3); all within the signed-in user's organization unless stated ----

    /// <summary>Filtered, sorted and paged in SQL. The query must already be validated (page, pageSize, sortBy).</summary>
    Task<PagedResult<UserListItemDto>> GetPageAsync(UserListQuery query, CancellationToken cancellationToken);

    /// <param name="now">For <see cref="UserDetailsDto.IsLockedOut"/>.</param>
    Task<UserDetailsDto?> GetDetailsAsync(int userId, DateTime now, CancellationToken cancellationToken);

    /// <summary>
    /// Whether any user in the whole system (all organizations – e-mail is a global login identifier) has this e-mail.
    /// Returns only a bool, so no data of another organization leaves this method.
    /// </summary>
    Task<bool> EmailExistsAsync(string email, int? excludeUserId, CancellationToken cancellationToken);

    /// <summary>Next value of the global employee-code sequence (atomic, safe under concurrency; gaps are possible).</summary>
    Task<long> NextEmployeeNumberAsync(CancellationToken cancellationToken);

    /// <summary>Active users with the Admin role, optionally not counting one user.</summary>
    Task<int> CountActiveAdminsAsync(int? excludeUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Exclusive lock on "the admins of this organization" until the current transaction ends, so two concurrent
    /// requests cannot both pass the last-admin check. Must run inside <see cref="IUnitOfWork.ExecuteInTransactionAsync{T}"/>.
    /// </summary>
    /// <exception cref="Common.Exceptions.ConflictException">The lock could not be acquired in time.</exception>
    Task LockOrganizationAdminsAsync(int organizationId, CancellationToken cancellationToken);

    /// <summary>Stages a new user; written by <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
    void Add(User user);
}
