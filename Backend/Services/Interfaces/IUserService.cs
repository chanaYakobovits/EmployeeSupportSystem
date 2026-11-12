using Common.DTOs.Paging;
using Common.DTOs.Users;

namespace Services.Interfaces;

/// <summary>
/// User administration (spec 4.2, 7.8, 9.3), for the Admin role. Every method works within the signed-in admin's
/// organization: a user of another organization is "not found" (→ 404).
/// <paramref name="ipAddress"/> parameters are recorded in the audit log only.
/// </summary>
public interface IUserService
{
    /// <exception cref="Common.Exceptions.ValidationException">Invalid page, pageSize, sortBy or filter (→ 400).</exception>
    Task<PagedResult<UserListItemDto>> GetPageAsync(UserListQuery query, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.NotFoundException"/>
    Task<UserDetailsDto> GetAsync(int userId, CancellationToken cancellationToken);

    /// <summary>
    /// Generates the employee code and a temporary password (returned only here), MustChangePassword = true.
    /// </summary>
    /// <exception cref="Common.Exceptions.ConflictException">The e-mail is already registered (→ 409).</exception>
    Task<CreateUserResponse> CreateAsync(CreateUserRequest request, string ipAddress, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.BusinessRuleException">A protection rule was broken (→ 422).</exception>
    Task<UserDetailsDto> UpdateAsync(int userId, UpdateUserRequest request, string ipAddress, CancellationToken cancellationToken);

    /// <summary>New temporary password, MustChangePassword = true, lockout cleared, all refresh tokens revoked.</summary>
    Task<ResetPasswordResponse> ResetPasswordAsync(int userId, string ipAddress, CancellationToken cancellationToken);

    /// <summary>
    /// Deactivation revokes all refresh tokens and returns the user's unresolved tickets to their department queue.
    /// </summary>
    Task<SetUserActiveResponse> SetActiveAsync(int userId, SetUserActiveRequest request, string ipAddress,
        CancellationToken cancellationToken);
}
