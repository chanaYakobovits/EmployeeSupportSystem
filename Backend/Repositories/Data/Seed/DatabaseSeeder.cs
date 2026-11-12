using Common.Enums;
using Common.Interfaces;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Repositories.Data.Seed;

/// <summary>
/// Drops the database, recreates it from the migrations and fills it with consistent demo data (spec 8.5).
/// Deterministic: a fixed Random seed, and all dates relative to one "now" captured at the start.
/// The caller (API, --seed) is responsible for allowing this only in Development.
/// </summary>
internal sealed class DatabaseSeeder(AppDbContext db, IPasswordHasher passwordHasher, ILogger<DatabaseSeeder> logger)
    : IDatabaseSeeder
{
    private const int RandomSeed = 1042;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        // Validate the demo data before touching the database.
        ValidateManagerRoles();

        logger.LogWarning("Dropping and recreating database {Database}", db.Database.GetDbConnection().Database);
        await db.Database.EnsureDeletedAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);

        var utcNow = DateTime.UtcNow;
        var now = new DateTime(utcNow.Ticks - utcNow.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
        var random = new Random(RandomSeed);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Organization and departments (managers are set after the users exist: circular relationship).
        var organization = new Organization
        {
            Name = OrganizationSeedData.OrganizationName,
            IsActive = true,
            CreatedAt = now.AddDays(-730)
        };
        db.Organizations.Add(organization);

        var departments = OrganizationSeedData.Departments.ToDictionary(
            d => d.Key,
            d => new Department { Organization = organization, Name = d.Name, IsActive = true });
        db.Departments.AddRange(departments.Values);
        await db.SaveChangesAsync(cancellationToken);

        // Users. Every user is also an Employee (spec 2.3).
        var users = new List<SeededUser>();
        for (var i = 0; i < OrganizationSeedData.People.Length; i++)
        {
            var person = OrganizationSeedData.People[i];
            var user = new User
            {
                OrganizationId = organization.Id,
                DepartmentId = departments[person.Department].Id,
                EmployeeCode = OrganizationSeedData.EmployeeCode(i + 1),
                FirstName = person.FirstName,
                LastName = person.LastName,
                Email = $"{person.EmailLocalPart}@{OrganizationSeedData.EmailDomain}",
                JobTitle = person.JobTitle,
                PasswordHash = passwordHasher.Hash(OrganizationSeedData.DemoPassword),
                MustChangePassword = person.MustChangePassword,
                IsActive = person.IsActive,
                CreatedAt = person.MustChangePassword ? now.AddDays(-2) : now.AddDays(-400 + i),
                LastLoginAt = person.MustChangePassword ? null
                    : person.IsActive ? now.AddMinutes(-random.Next(30, 72 * 60))
                    : now.AddDays(-38),
                Roles = new[] { Role.Employee }.Concat(person.ExtraRoles)
                    .Select(role => new UserRole { Role = role })
                    .ToList()
            };
            users.Add(new SeededUser(user, person));
        }

        db.Users.AddRange(users.Select(u => u.User));
        await db.SaveChangesAsync(cancellationToken);

        // The demo users have fixed codes (README); new users continue after the last one.
        await db.Database.ExecuteSqlRawAsync(EmployeeCodeSequence.RestartAfterExistingCodesSql, cancellationToken);

        foreach (var manager in users.Where(u => u.Person.IsDepartmentManager))
        {
            departments[manager.Person.Department].ManagerUserId = manager.User.Id;
        }

        // Categories and routing (spec 3.5), SLA policies (spec 3.4).
        var categories = ReferenceSeedData.Categories.ToDictionary(
            c => c.Code,
            c => new Category
            {
                OrganizationId = organization.Id,
                Code = c.Code,
                Name = c.Name,
                Description = c.Description,
                RoutingType = c.RoutingType,
                HandlingDepartmentId = c.HandlingDepartment is { } key ? departments[key].Id : null,
                IsFallback = c.IsFallback,
                IsActive = true
            });
        db.Categories.AddRange(categories.Values);

        db.SlaPolicies.AddRange(ReferenceSeedData.SlaHours.Select(sla => new SlaPolicy
        {
            OrganizationId = organization.Id,
            Urgency = sla.Key,
            ResolutionHours = sla.Value
        }));
        await db.SaveChangesAsync(cancellationToken);

        // Tickets with their history, comments and AI analysis.
        var generator = new TicketSeedGenerator(
            new SeedOrganization
            {
                OrganizationId = organization.Id,
                Departments = departments,
                Users = users,
                Categories = categories
            },
            random,
            now);

        // Counted from the list: a query would return nothing here, since no user is signed in (organization filter).
        var tickets = generator.Generate();
        db.Tickets.AddRange(tickets);
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        // Counts only – never ticket content (spec 6.4).
        logger.LogInformation(
            "Seed completed: {Users} users, {Departments} departments, {Categories} categories, {Tickets} tickets",
            users.Count, departments.Count, categories.Count, tickets.Count);
    }

    /// <summary>
    /// Rule: every department manager (Department.ManagerUserId) holds the DepartmentManager role,
    /// except the CEO who manages the Management department – the Executive role covers it.
    /// Each department has exactly one manager.
    /// </summary>
    private static void ValidateManagerRoles()
    {
        var errors = new List<string>();

        foreach (var department in OrganizationSeedData.Departments)
        {
            var managers = OrganizationSeedData.People
                .Where(p => p.IsDepartmentManager && p.Department == department.Key)
                .ToList();

            if (managers.Count != 1)
            {
                errors.Add($"Department {department.Key} must have exactly one manager, found {managers.Count}.");
                continue;
            }

            var roles = managers[0].ExtraRoles;
            var isManagement = department.Key == DepartmentKey.Management;

            if (isManagement && (!roles.Contains(Role.Executive) || roles.Contains(Role.DepartmentManager)))
            {
                errors.Add($"The manager of {department.Key} must be Executive and not DepartmentManager.");
            }
            else if (!isManagement && !roles.Contains(Role.DepartmentManager))
            {
                errors.Add($"The manager of {department.Key} must hold the DepartmentManager role.");
            }
        }

        errors.AddRange(OrganizationSeedData.People
            .Where(p => !p.IsDepartmentManager && p.ExtraRoles.Contains(Role.DepartmentManager))
            .Select(p => $"{p.EmailLocalPart} has the DepartmentManager role but manages no department."));

        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Invalid seed data: " + string.Join(" ", errors));
        }
    }
}
