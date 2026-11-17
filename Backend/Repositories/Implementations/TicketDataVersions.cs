using System.Collections.Concurrent;
using Common.Interfaces;

namespace Repositories.Implementations;

/// <summary>
/// <see cref="ITicketDataVersion"/> in the server's memory (Singleton), like the dashboard cache it keys (spec 11.3,
/// 13.1). Starts at 0 for every organization; a restart empties the cache too, so restarting the count is safe.
/// </summary>
public sealed class TicketDataVersions : ITicketDataVersion
{
    private readonly ConcurrentDictionary<int, long> _versions = new();

    public long Get(int organizationId) => _versions.TryGetValue(organizationId, out var version) ? version : 0;

    public void Bump(int organizationId) => _versions.AddOrUpdate(organizationId, 1, (_, version) => version + 1);
}
