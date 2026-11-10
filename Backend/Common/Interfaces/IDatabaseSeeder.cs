namespace Common.Interfaces;

/// <summary>
/// Drops, recreates and fills the database with demo data (spec 8.5). Development only.
/// </summary>
public interface IDatabaseSeeder
{
    Task SeedAsync(CancellationToken cancellationToken);
}
