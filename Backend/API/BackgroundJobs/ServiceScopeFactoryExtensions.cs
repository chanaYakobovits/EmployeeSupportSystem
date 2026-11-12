using Common.Interfaces;

namespace API.BackgroundJobs;

public static class ServiceScopeFactoryExtensions
{
    /// <summary>
    /// A new scope bound to one organization: everything resolved from it (DbContext, repositories, services) sees
    /// exactly that organization's rows, as in a request of a user of it. For background jobs, which run without a
    /// signed-in user (without this the query filters return no rows at all). One scope per organization – never
    /// a general IgnoreQueryFilters.
    /// </summary>
    public static AsyncServiceScope CreateOrganizationScope(this IServiceScopeFactory scopeFactory, int organizationId)
    {
        var scope = scopeFactory.CreateAsyncScope();
        try
        {
            scope.ServiceProvider.GetRequiredService<ISystemOrganizationContext>().Enter(organizationId);
            return scope;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }
}
