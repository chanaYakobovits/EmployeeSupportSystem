using Common.Enums;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Repositories.Data;
using Repositories.Implementations;
using Services.Implementations;
using Services.Tests.Auth.Fakes;

namespace Services.Tests.Data;

/// <summary>
/// Spec 6.2: the global query filter by OrganizationId, run as real queries (EF Core InMemory provider)
/// over data of two organizations. Checks isolation, fail-closed without a signed-in user, and the two
/// places that bypass the filter on purpose.
/// </summary>
public sealed class OrganizationQueryFilterTests
{
    private const int OrgA = 1;
    private const int OrgB = 2;
    private const string TokenHashB = "HASH-OF-ORG-B-TOKEN";

    private readonly string _databaseName = Guid.NewGuid().ToString();

    public OrganizationQueryFilterTests()
    {
        using var db = CreateContext(signedInOrganizationId: null);
        SeedOrganization(db, OrgA);
        SeedOrganization(db, OrgB);
        db.AuditLogs.Add(new AuditLog { Id = 99, OrganizationId = null, Action = "LoginFailed.UnknownUser", EntityType = "User", IpAddress = "x" });
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = 1, UserId = UserIdOf(OrgB), TokenHash = TokenHashB, CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(7),
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task Every_entity_with_OrganizationId_has_a_query_filter()
    {
        await using var db = CreateContext(OrgA);

        var unfiltered = db.Model.GetEntityTypes()
            .Where(e => e.FindProperty("OrganizationId") is not null || e.ClrType == typeof(Organization))
            .Where(e => e.GetQueryFilter() is null)
            .Select(e => e.ClrType.Name)
            .ToList();

        // The one deliberate exception: the worker claims queue rows of every organization (ids only), then enters
        // each job's organization.
        Assert.Equal([nameof(InvitationEmailJob)], unfiltered);
    }

    // ---- explicit organization for background jobs (no signed-in user) ----

    [Fact]
    public async Task A_background_scope_that_entered_an_organization_sees_only_that_organization()
    {
        await using var db = CreateBackgroundContext(enter: OrgB);

        Assert.Equal([OrgB], await db.Organizations.Select(o => o.Id).ToListAsync());
        Assert.Equal([UserIdOf(OrgB)], await db.Users.Select(u => u.Id).ToListAsync());
        Assert.Equal([TicketIdOf(OrgB)], await db.Tickets.Select(t => t.Id).ToListAsync());
        Assert.Null(await new UserRepository(db).FindForUpdateAsync(UserIdOf(OrgA), CancellationToken.None));
        Assert.NotNull(await new UserRepository(db).FindForUpdateAsync(UserIdOf(OrgB), CancellationToken.None));
    }

    [Fact]
    public async Task A_background_scope_that_entered_no_organization_sees_nothing()
    {
        await using var db = CreateBackgroundContext(enter: null);

        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.Tickets.ToListAsync());
    }

    [Fact]
    public void Entering_an_organization_is_refused_when_a_user_is_signed_in()
    {
        var context = new OrganizationContext(new FakeCurrentUser { SignedInUserId = 1, SignedInOrganizationId = OrgA });

        Assert.Throws<InvalidOperationException>(() => context.Enter(OrgB));
        Assert.Equal(OrgA, context.OrganizationId);
    }

    [Fact]
    public void A_scope_cannot_switch_to_another_organization()
    {
        var context = new OrganizationContext(new FakeCurrentUser());
        context.Enter(OrgA);
        context.Enter(OrgA); // the same one again is harmless

        Assert.Throws<InvalidOperationException>(() => context.Enter(OrgB));
        Assert.Equal(OrgA, context.OrganizationId);
    }

    [Fact]
    public async Task User_of_organization_A_sees_only_rows_of_organization_A()
    {
        await using var db = CreateContext(OrgA);

        Assert.Equal([OrgA], await db.Organizations.Select(o => o.Id).ToListAsync());
        Assert.All(await db.Departments.ToListAsync(), d => Assert.Equal(OrgA, d.OrganizationId));
        Assert.All(await db.Users.ToListAsync(), u => Assert.Equal(OrgA, u.OrganizationId));
        Assert.All(await db.Categories.ToListAsync(), c => Assert.Equal(OrgA, c.OrganizationId));
        Assert.All(await db.SlaPolicies.ToListAsync(), s => Assert.Equal(OrgA, s.OrganizationId));
        Assert.All(await db.Tickets.ToListAsync(), t => Assert.Equal(OrgA, t.OrganizationId));
        Assert.All(await db.AuditLogs.ToListAsync(), a => Assert.Equal(OrgA, a.OrganizationId));

        // Not vacuous: organization A really has rows of every kind.
        Assert.Equal(1, await db.Departments.CountAsync());
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(1, await db.Categories.CountAsync());
        Assert.Equal(1, await db.SlaPolicies.CountAsync());
        Assert.Equal(1, await db.Tickets.CountAsync());
        Assert.Equal(1, await db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task Rows_of_another_organization_cannot_be_found_even_by_id()
    {
        await using var db = CreateContext(OrgA);

        Assert.Null(await db.Users.SingleOrDefaultAsync(u => u.Id == UserIdOf(OrgB)));
        Assert.Null(await db.Tickets.SingleOrDefaultAsync(t => t.Id == TicketIdOf(OrgB)));
        Assert.Null(await new UserRepository(db).GetCurrentUserAsync(UserIdOf(OrgB), CancellationToken.None));
        Assert.Null(await new UserRepository(db).FindForUpdateAsync(UserIdOf(OrgB), CancellationToken.None));
    }

    [Fact]
    public async Task Without_a_signed_in_user_no_rows_are_returned_at_all()
    {
        await using var db = CreateContext(signedInOrganizationId: null);

        Assert.Empty(await db.Organizations.ToListAsync());
        Assert.Empty(await db.Departments.ToListAsync());
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.Categories.ToListAsync());
        Assert.Empty(await db.SlaPolicies.ToListAsync());
        Assert.Empty(await db.Tickets.ToListAsync());
        // Includes the row whose OrganizationId is null: null must not match "no organization".
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    // ---- the two deliberate bypasses ----

    [Fact]
    public async Task Login_lookup_finds_users_of_any_organization_without_a_signed_in_user()
    {
        await using var db = CreateContext(signedInOrganizationId: null);
        var users = new UserRepository(db);

        var byEmail = await users.FindForLoginByEmailAsync(EmailOf(OrgB), CancellationToken.None);
        var byCode = await users.FindForLoginByEmployeeCodeAsync(CodeOf(OrgA), CancellationToken.None);

        Assert.Equal(UserIdOf(OrgB), byEmail?.Id);
        Assert.Equal("Dept " + OrgB, byEmail?.Department.Name);
        Assert.Equal(UserIdOf(OrgA), byCode?.Id);
    }

    [Fact]
    public async Task Refresh_token_lookup_works_without_a_signed_in_user_and_includes_the_user()
    {
        await using var db = CreateContext(signedInOrganizationId: null);

        // Why the bypass is needed: the same query with the filter loses the token (filtered INNER JOIN to Users).
        Assert.Null(await db.RefreshTokens.Include(rt => rt.User).SingleOrDefaultAsync(rt => rt.TokenHash == TokenHashB));

        var token = await new RefreshTokenRepository(db).FindByHashAsync(TokenHashB, CancellationToken.None);

        Assert.NotNull(token);
        Assert.Equal(UserIdOf(OrgB), token.User.Id);
        Assert.Equal([Role.Employee], token.User.Roles.Select(r => r.Role));
        Assert.Equal("Dept " + OrgB, token.User.Department.Name);
    }

    // ---- setup ----

    private AppDbContext CreateContext(int? signedInOrganizationId)
    {
        var currentUser = new FakeCurrentUser();
        if (signedInOrganizationId is { } org)
        {
            currentUser.SignedInUserId = UserIdOf(org);
            currentUser.SignedInOrganizationId = org;
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .Options;
        return new AppDbContext(options, new OrganizationContext(currentUser));
    }

    /// <summary>As a background job's scope: nobody signed in, optionally an organization entered explicitly.</summary>
    private AppDbContext CreateBackgroundContext(int? enter)
    {
        var organizationContext = new OrganizationContext(new FakeCurrentUser());
        if (enter is { } org)
        {
            organizationContext.Enter(org);
        }

        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_databaseName).Options;
        return new AppDbContext(options, organizationContext);
    }

    private static int UserIdOf(int org) => org * 10;
    private static int TicketIdOf(int org) => org * 100;
    private static string EmailOf(int org) => $"user{org}@org{org}.example.com";
    private static string CodeOf(int org) => $"EMP-{org:00000}";

    private static void SeedOrganization(AppDbContext db, int org)
    {
        var now = DateTime.UtcNow;
        db.Organizations.Add(new Organization { Id = org, Name = "Org " + org, IsActive = true, CreatedAt = now });
        db.Departments.Add(new Department { Id = org, OrganizationId = org, Name = "Dept " + org, IsActive = true });
        db.Users.Add(new User
        {
            Id = UserIdOf(org), OrganizationId = org, DepartmentId = org, EmployeeCode = CodeOf(org),
            FirstName = "F", LastName = "L", Email = EmailOf(org), JobTitle = "J", PasswordHash = "h",
            IsActive = true, CreatedAt = now,
            Roles = [new UserRole { UserId = UserIdOf(org), Role = Role.Employee }],
        });
        db.Categories.Add(new Category { Id = org, OrganizationId = org, Code = "GENERAL" + org, Name = "General", IsActive = true });
        db.SlaPolicies.Add(new SlaPolicy { Id = org, OrganizationId = org, Urgency = Urgency.Medium, ResolutionHours = 24 });
        db.Tickets.Add(new Ticket
        {
            Id = TicketIdOf(org), OrganizationId = org, RequesterId = UserIdOf(org), RequesterDepartmentId = org,
            Description = "text", Status = TicketStatus.New, CreatedAt = now, UpdatedAt = now, RowVersion = [1],
        });
        db.AuditLogs.Add(new AuditLog
        {
            Id = org, OrganizationId = org, UserId = UserIdOf(org), Action = "LoginSucceeded", EntityType = "User",
            EntityId = UserIdOf(org), IpAddress = "x", CreatedAt = now,
        });
    }
}
