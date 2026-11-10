using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Repositories.Data;

namespace Repositories.Extensions;

public static class DependencyInjection
{
    private const string ConnectionStringName = "DefaultConnection";

    /// <summary>
    /// Registers the data-access layer (DbContext, repositories).
    /// Called only by the Services layer, never directly by API.
    /// </summary>
    public static IServiceCollection AddRepositories(this IServiceCollection services, IConfiguration configuration)
    {
        // The connection string lives only in user-secrets (dev) or environment variables (server) – spec 6.5.
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is missing. Set it with: " +
                $"dotnet user-secrets set \"ConnectionStrings:{ConnectionStringName}\" \"<value>\" --project API");
        }

        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString)); // Scoped

        return services;
    }
}
