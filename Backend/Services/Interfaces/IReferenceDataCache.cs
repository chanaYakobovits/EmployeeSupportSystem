namespace Services.Interfaces;

/// <summary>Organization reference data kept in the cache (spec 11.3).</summary>
public enum ReferenceData
{
    Departments,
    Categories
}

/// <summary>
/// Cache of an organization's reference data (spec 11.3): one entry per organization and kind, holding the full
/// list (active and inactive), 10 minutes. Every change to the data removes the organization's entry.
/// Behind an interface so a multi-server deployment can move to IDistributedCache / HybridCache without
/// changing the services. The organization is explicit: background jobs have no signed-in user.
/// </summary>
public interface IReferenceDataCache
{
    Task<T> GetOrCreateAsync<T>(ReferenceData kind, int organizationId, Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken) where T : class;

    void Invalidate(ReferenceData kind, int organizationId);
}
