using Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Repositories.Data;
using Repositories.Data.Seed;
using Repositories.Implementations;
using Repositories.Interfaces;

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

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Demo data (spec 8.5). Exposed only through the Common interface; API allows it only in Development.
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();

        return services;
    }
}
