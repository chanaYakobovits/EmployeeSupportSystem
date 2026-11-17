namespace Services.Interfaces;

/// <summary>Organization reference data kept in the cache (spec 11.3).</summary>
public enum ReferenceData
{
    Departments,
    Categories,

    /// <summary>Resolution hours by urgency (task 3.3: read on every new ticket).</summary>
    SlaPolicies,

    /// <summary>
    /// "First Last" of every user of the organization, for PII redaction before the AI (task 4.3, spec 5.3).
    /// A shorter lifetime than the rest (5 minutes); UserService removes it on every change to a user.
    /// </summary>
    UserNames
}

/// <summary>
/// Cache of an organization's reference data (spec 11.3): one entry per organization and kind, holding the full
/// list (active and inactive), 10 minutes (user names: 5). Every change to the data removes the organization's entry.
/// Behind an interface so a multi-server deployment can move to IDistributedCache / HybridCache without
/// changing the services. The organization is explicit: background jobs have no signed-in user.
/// </summary>
public interface IReferenceDataCache
{
    Task<T> GetOrCreateAsync<T>(ReferenceData kind, int organizationId, Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken) where T : class;

    void Invalidate(ReferenceData kind, int organizationId);
}
