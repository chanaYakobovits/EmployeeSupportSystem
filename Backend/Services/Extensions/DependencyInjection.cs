using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Repositories.Extensions;

namespace Services.Extensions;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the business layer and, through it, the layer below (Repositories).
    /// API calls only this method, so it never needs a reference to Repositories.
    /// </summary>
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRepositories(configuration);

        // Later tasks: services (Scoped); PiiRedactor, TicketStatusRules, IDateTimeProvider (Singleton).
        return services;
    }
}
