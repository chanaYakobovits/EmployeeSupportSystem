using Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using Repositories.Data;
using Repositories.Interfaces;

namespace Repositories.Implementations;

public sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    /// <exception cref="ConflictException">A concurrency token did not match (the row was changed in the meantime).</exception>
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Translated here so the Services layer never depends on EF exception types.
            throw new ConflictException(errorType: "concurrency");
        }
    }
}
