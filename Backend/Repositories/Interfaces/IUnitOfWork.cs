namespace Repositories.Interfaces;

/// <summary>Saves everything staged by the repositories of the current scope in one transaction.</summary>
public interface IUnitOfWork
{
    /// <exception cref="Common.Exceptions.ConflictException">Concurrent update of the same row.</exception>
    /// <exception cref="Common.Exceptions.UniqueConstraintException">A unique index was violated.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs <paramref name="action"/> in one database transaction and commits it if the action completes.
    /// Needed when the action takes a lock (e.g. <see cref="IUserRepository.LockOrganizationAdminsAsync"/>)
    /// or mixes SaveChanges with bulk updates that must succeed or fail together.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
}
