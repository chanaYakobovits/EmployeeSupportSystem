namespace Repositories.Interfaces;

/// <summary>Saves everything staged by the repositories of the current scope in one transaction.</summary>
public interface IUnitOfWork
{
    /// <exception cref="Common.Exceptions.ConflictException">Concurrent update of the same row.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
