namespace Common.Interfaces;

/// <summary>
/// A version number per organization that changes whenever any of its tickets changes (spec 11.3). Part of the
/// dashboard cache keys: a new version makes every cached dashboard of the organization unused, without finding and
/// removing them. Bumped by the data layer on every saved ticket change (TicketChangesInterceptor) – services never
/// bump it themselves. Local to the server, like the cache (spec 13.1).
/// </summary>
public interface ITicketDataVersion
{
    long Get(int organizationId);

    void Bump(int organizationId);
}
