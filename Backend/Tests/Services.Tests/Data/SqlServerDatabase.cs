using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Repositories.Data;
using Services.Implementations;
using Services.Tests.Auth.Fakes;

namespace Services.Tests.Data;

/// <summary>
/// A real SQL Server database for the queries InMemory cannot run (DATEDIFF, AT TIME ZONE, ExecuteUpdate, transactions).
/// The server comes from the environment variable <see cref="ConnectionStringVariable"/> (any database name in it is
/// replaced); without it the tests are skipped (<see cref="SqlServerFactAttribute"/>), so <c>dotnet test</c> still passes.
/// Example: <c>Server=.\SQLEXPRESS;Trusted_Connection=True;TrustServerCertificate=True</c>.
/// One temporary database per test class (all migrations applied), dropped at the end. Tests isolate their data by
/// seeding an organization of their own – the query filters do the rest.
/// </summary>
public sealed class SqlServerDatabase : IAsyncLifetime
{
    public const string ConnectionStringVariable = "ESS_TEST_SQLSERVER";

    public static string? ServerConnectionString =>
        Environment.GetEnvironmentVariable(ConnectionStringVariable) is { Length: > 0 } value ? value : null;

    private readonly string? _connectionString = ServerConnectionString is { } server
        ? new SqlConnectionStringBuilder(server) { InitialCatalog = $"EssTests_{Guid.NewGuid():N}" }.ConnectionString
        : null;

    public async Task InitializeAsync()
    {
        if (_connectionString is null)
        {
            return;
        }

        await using var db = CreateContext(signedInUserId: null, organizationId: 0);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_connectionString is null)
        {
            return;
        }

        await using var db = CreateContext(signedInUserId: null, organizationId: 0);
        await db.Database.EnsureDeletedAsync();
    }

    /// <summary>A fresh context of a request by <paramref name="signedInUserId"/> (null: nobody – filters return nothing).</summary>
    public AppDbContext CreateContext(int? signedInUserId, int organizationId, params IInterceptor[] interceptors)
    {
        var currentUser = new FakeCurrentUser { SignedInUserId = signedInUserId, SignedInOrganizationId = organizationId };
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(_connectionString ?? throw new InvalidOperationException($"{ConnectionStringVariable} is not set."))
            .AddInterceptors(interceptors)
            .Options;
        return new AppDbContext(options, new OrganizationContext(currentUser));
    }
}

/// <summary>A test that needs <see cref="SqlServerDatabase"/>: skipped when its environment variable is not set.</summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (SqlServerDatabase.ServerConnectionString is null)
        {
            Skip = $"Set {SqlServerDatabase.ConnectionStringVariable} to a SQL Server connection string to run this test.";
        }
    }
}
