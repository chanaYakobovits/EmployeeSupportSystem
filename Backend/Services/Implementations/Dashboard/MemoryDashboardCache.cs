using Microsoft.Extensions.Caching.Memory;
using Services.Interfaces;

namespace Services.Implementations.Dashboard;

/// <summary><see cref="IDashboardCache"/> over the local <see cref="IMemoryCache"/> (one server, spec 13.1).</summary>
public sealed class MemoryDashboardCache(IMemoryCache cache) : IDashboardCache
{
    /// <summary>Spec 11.3: 60 seconds – the version in the key invalidates earlier after a ticket change.</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(60);

    public async Task<(T Value, bool Hit)> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken) where T : class
    {
        if (cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            return (cached, true);
        }

        // Two simultaneous misses compute twice; both results are equal and the second replaces the first.
        var value = await factory(cancellationToken);
        cache.Set(key, value, Duration);
        return (value, false);
    }
}
