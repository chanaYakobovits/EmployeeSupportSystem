using Common.Models;
using Microsoft.EntityFrameworkCore;
using Repositories.Data;
using Repositories.Interfaces;

namespace Repositories.Implementations;

public sealed class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    public void Add(RefreshToken refreshToken) => db.RefreshTokens.Add(refreshToken);

    // IgnoreQueryFilters: refresh/logout carry no access token, so nobody is signed in. Without it the
    // Include of the (filtered, required) User becomes a filtered INNER JOIN and the token is never found.
    // Safe: the 256-bit token value itself is the proof of identity.
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .IgnoreQueryFilters()
            .Include(rt => rt.User).ThenInclude(u => u.Roles)
            .Include(rt => rt.User).ThenInclude(u => u.Department)
            .SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

    // RefreshTokens has no OrganizationId and is not filtered: no IgnoreQueryFilters needed.
    public async Task<IReadOnlyList<RefreshToken>> GetActiveForUserAsync(int userId, DateTime now,
        CancellationToken cancellationToken) =>
        await db.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > now)
            .ToListAsync(cancellationToken);
}
