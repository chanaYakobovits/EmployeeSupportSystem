using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Repositories.Extensions;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the data-access layer (DbContext, repositories).
    /// Called only by the Services layer, never directly by API.
    /// </summary>
    public static IServiceCollection AddRepositories(this IServiceCollection services, IConfiguration configuration)
    {
        // Task 1.4: AppDbContext (Scoped) + repositories (Scoped).
        return services;
    }
}
