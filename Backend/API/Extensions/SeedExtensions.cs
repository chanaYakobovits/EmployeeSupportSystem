using Common.Interfaces;

namespace API.Extensions;

public static class SeedExtensions
{
    public const string SeedArgument = "--seed";

    public static bool IsSeedCommand(this string[] args) =>
        args.Contains(SeedArgument, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Drops, recreates and fills the database with demo data (spec 8.5).
    /// Refuses to run outside Development. Returns the process exit code.
    /// </summary>
    public static async Task<int> RunSeedAsync(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Seed");

        if (!app.Environment.IsDevelopment())
        {
            logger.LogError(
                "Seeding refused: it drops and recreates the database and is allowed only in Development. Current environment: {Environment}",
                app.Environment.EnvironmentName);
            return 1;
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };

        await using var scope = app.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
        await seeder.SeedAsync(cancellation.Token);

        return 0;
    }
}
