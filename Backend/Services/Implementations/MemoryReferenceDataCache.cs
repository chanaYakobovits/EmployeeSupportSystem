using Microsoft.Extensions.Caching.Memory;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary><see cref="IReferenceDataCache"/> over the local <see cref="IMemoryCache"/> (one server).</summary>
public sealed class MemoryReferenceDataCache(IMemoryCache cache) : IReferenceDataCache
{
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(10);

    public async Task<T> GetOrCreateAsync<T>(ReferenceData kind, int organizationId,
        Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken) where T : class
    {
        var key = Key(kind, organizationId);
        if (cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            return cached;
        }

        var value = await factory(cancellationToken);
        cache.Set(key, value, Duration);
        return value;
    }

    public void Invalidate(ReferenceData kind, int organizationId) => cache.Remove(Key(kind, organizationId));

    internal static string Key(ReferenceData kind, int organizationId) => kind switch
    {
        ReferenceData.Departments => $"departments:{organizationId}",
        ReferenceData.Categories => $"categories:{organizationId}",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
