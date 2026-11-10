namespace Repositories.Interfaces;

/// <summary>Saves everything staged by the repositories of the current scope in one transaction.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
