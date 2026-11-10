using Repositories.Data;
using Repositories.Interfaces;

namespace Repositories.Implementations;

public sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
