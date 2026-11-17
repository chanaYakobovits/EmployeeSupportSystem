using Microsoft.Extensions.Caching.Memory;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary><see cref="IReferenceDataCache"/> over the local <see cref="IMemoryCache"/> (one server).</summary>
public sealed class MemoryReferenceDataCache(IMemoryCache cache) : IReferenceDataCache
{
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(10);

    /// <summary>User names change with the users list, which is not cached otherwise – a shorter lifetime.</summary>
    public static readonly TimeSpan UserNamesDuration = TimeSpan.FromMinutes(5);

    public async Task<T> GetOrCreateAsync<T>(ReferenceData kind, int organizationId,
        Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken) where T : class
    {
        var key = Key(kind, organizationId);
        if (cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            return cached;
        }

        var value = await factory(cancellationToken);
        cache.Set(key, value, kind == ReferenceData.UserNames ? UserNamesDuration : Duration);
        return value;
    }

    public void Invalidate(ReferenceData kind, int organizationId) => cache.Remove(Key(kind, organizationId));

    internal static string Key(ReferenceData kind, int organizationId) => kind switch
    {
        ReferenceData.Departments => $"departments:{organizationId}",
        ReferenceData.Categories => $"categories:{organizationId}",
        ReferenceData.SlaPolicies => $"sla:{organizationId}",
        ReferenceData.UserNames => $"user-names:{organizationId}",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
