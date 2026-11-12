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
    /// Generates the employee code; no password (MustChangePassword = true). An invitation e-mail is queued in the same
    /// transaction – the response shows it as Pending.
    /// </summary>
    /// <exception cref="Common.Exceptions.ConflictException">The e-mail is already registered (→ 409).</exception>
    Task<UserDetailsDto> CreateAsync(CreateUserRequest request, string ipAddress, CancellationToken cancellationToken);

    /// <summary>
    /// Sends the invitation again: earlier links are revoked, a queued e-mail is cancelled, a new one is queued.
    /// </summary>
    /// <exception cref="Common.Exceptions.NotFoundException"/>
    /// <exception cref="Common.Exceptions.BusinessRuleException">
    /// The user already chose a password, or is inactive (→ 422).
    /// </exception>
    Task<UserDetailsDto> ResendInvitationAsync(int userId, string ipAddress, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.BusinessRuleException">A protection rule was broken (→ 422).</exception>
    Task<UserDetailsDto> UpdateAsync(int userId, UpdateUserRequest request, string ipAddress, CancellationToken cancellationToken);

    /// <summary>
    /// New temporary password, MustChangePassword = true, lockout cleared, all refresh tokens revoked,
    /// open invitations revoked.
    /// </summary>
    Task<ResetPasswordResponse> ResetPasswordAsync(int userId, string ipAddress, CancellationToken cancellationToken);

    /// <summary>
    /// Deactivation revokes all refresh tokens and returns the user's unresolved tickets to their department queue.
    /// </summary>
    Task<SetUserActiveResponse> SetActiveAsync(int userId, SetUserActiveRequest request, string ipAddress,
        CancellationToken cancellationToken);
}
