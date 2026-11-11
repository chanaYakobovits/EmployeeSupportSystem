using Common.Models;

namespace Repositories.Interfaces;

public interface IRefreshTokenRepository
{
    /// <summary>Stages a new token; written by <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
    void Add(RefreshToken refreshToken);

    /// <summary>
    /// Tracked token (active or not) with its User, Roles and Department. Null if no token has this hash.
    /// Works without a signed-in user (refresh and logout are anonymous).
    /// </summary>
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>Tracked, not revoked and not expired tokens of the user.</summary>
    Task<IReadOnlyList<RefreshToken>> GetActiveForUserAsync(int userId, DateTime now, CancellationToken cancellationToken);
}
