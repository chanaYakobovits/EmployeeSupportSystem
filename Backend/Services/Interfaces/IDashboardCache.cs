namespace Services.Interfaces;

/// <summary>
/// The computed dashboards (spec 11.3), 60 seconds per key. The key includes the organization's ticket data version
/// (ITicketDataVersion), so a ticket change makes the old entries unused – nothing is removed explicitly.
/// Behind an interface so a multi-server deployment can move to IDistributedCache / HybridCache without changing the
/// service (spec 11.3).
/// </summary>
public interface IDashboardCache
{
    /// <summary>The cached value of <paramref name="key"/>, or the factory's result – stored and returned.</summary>
    /// <returns>The value, and whether it came from the cache.</returns>
    Task<(T Value, bool Hit)> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken) where T : class;
}
