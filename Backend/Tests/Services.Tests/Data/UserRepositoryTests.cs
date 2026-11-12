using Common.DTOs.Users;
using Common.Enums;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Repositories.Data;
using Repositories.Implementations;
using Services.Implementations;
using Services.Tests.Auth.Fakes;

namespace Services.Tests.Data;

/// <summary>
/// GET /api/users as real queries (EF Core InMemory): search, filters, sorting, paging and organization isolation.
/// </summary>
public sealed class UserRepositoryTests
{
    private const int OrgA = 1;
    private const int OrgB = 2;
    private const int DevId = 10;
    private const int HrId = 11;

    private readonly string _databaseName = Guid.NewGuid().ToString();

    public UserRepositoryTests()
    {
        using var db = CreateContext(signedIn: false);
        db.Organizations.AddRange(
            new Organization { Id = OrgA, Name = "A", IsActive = true },
            new Organization { Id = OrgB, Name = "B", IsActive = true });
        db.Departments.AddRange(
            new Department { Id = DevId, OrganizationId = OrgA, Name = "פיתוח", IsActive = true },
            new Department { Id = HrId, OrganizationId = OrgA, Name = "משאבי אנוש", IsActive = true, ManagerUserId = 3 },
            new Department { Id = 20, OrganizationId = OrgB, Name = "פיתוח", IsActive = true });

        // 30 developers in A, plus a few specific users.
        for (var i = 1; i <= 30; i++)
        {
            db.Users.Add(NewUser(100 + i, OrgA, DevId, "מפתח", $"מספר{i:D2}", $"dev{i}@a.example.com"));
        }

        db.Users.Add(NewUser(1, OrgA, HrId, "יעל", "הרפז", "yael.harpaz@a.example.com", Role.Admin, Role.Handler));
        db.Users.Add(NewUser(2, OrgA, HrId, "עדי", "שמעוני", "adi@a.example.com", Role.Handler));
        db.Users.Add(NewUser(3, OrgA, HrId, "רון", "כהן", "ron@a.example.com", Role.DepartmentManager));
        var inactive = NewUser(4, OrgA, DevId, "אורית", "דנון", "orit@a.example.com");
        inactive.IsActive = false;
        db.Users.Add(inactive);
        db.Users.Add(NewUser(200, OrgB, 20, "יעל", "אחרת", "yael@b.example.com", Role.Admin));
        db.SaveChanges();
    }

    [Fact]
    public async Task Pages_have_the_right_size_and_total_count()
    {
        await using var db = CreateContext();
        var repository = new UserRepository(db);

        var first = await repository.GetPageAsync(new UserListQuery { PageSize = 25 }, DateTime.UtcNow, CancellationToken.None);
        var second = await repository.GetPageAsync(new UserListQuery { PageSize = 25, Page = 2 }, DateTime.UtcNow, CancellationToken.None);
        var beyond = await repository.GetPageAsync(new UserListQuery { PageSize = 25, Page = 9 }, DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(34, first.TotalCount);
        Assert.Equal(25, first.Items.Count);
        Assert.Equal(9, second.Items.Count);
        Assert.Empty(first.Items.Select(u => u.Id).Intersect(second.Items.Select(u => u.Id)));
        Assert.Empty(beyond.Items);
        Assert.Equal(34, beyond.TotalCount);
    }

    [Fact]
    public async Task Users_of_another_organization_are_never_listed()
    {
        await using var db = CreateContext();

        var page = await new UserRepository(db).GetPageAsync(new UserListQuery { Search = "יעל", PageSize = 100 },
            DateTime.UtcNow, CancellationToken.None);

        Assert.Equal([1], page.Items.Select(u => u.Id));
    }

    [Theory]
    [InlineData("הרפז", 1)]          // last name
    [InlineData("יעל הרפז", 1)]      // full name
    [InlineData("yael.harpaz", 1)]   // e-mail (InMemory compares case-sensitively; SQL Server's collation does not)
    [InlineData("EMP-00002", 2)]     // employee code
    public async Task Search_matches_name_full_name_email_and_employee_code(string search, int expectedId)
    {
        await using var db = CreateContext();

        var page = await new UserRepository(db).GetPageAsync(new UserListQuery { Search = search }, DateTime.UtcNow, CancellationToken.None);

        Assert.Equal([expectedId], page.Items.Select(u => u.Id));
    }

    [Fact]
    public async Task Filters_by_department_role_and_active_combine()
    {
        await using var db = CreateContext();
        var repository = new UserRepository(db);

        var handlersInHr = await repository.GetPageAsync(
            new UserListQuery { DepartmentId = HrId, Role = Role.Handler, SortBy = UserSortFields.Name },
            DateTime.UtcNow, CancellationToken.None);
        var inactive = await repository.GetPageAsync(new UserListQuery { IsActive = false }, DateTime.UtcNow, CancellationToken.None);

        Assert.Equal([1, 2], handlersInHr.Items.Select(u => u.Id)); // by last name: הרפז, שמעוני
        Assert.Equal([4], inactive.Items.Select(u => u.Id));
    }

    [Fact]
    public async Task Default_order_is_employee_code_descending_newest_first()
    {
        await using var db = CreateContext();

        var page = await new UserRepository(db).GetPageAsync(new UserListQuery { PageSize = 3 }, DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(["EMP-00130", "EMP-00129", "EMP-00128"], page.Items.Select(u => u.EmployeeCode));
    }

    [Fact]
    public async Task Default_column_sorts_ascending_when_asked()
    {
        await using var db = CreateContext();

        var page = await new UserRepository(db).GetPageAsync(new UserListQuery { SortDir = "asc", PageSize = 3 },
            DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(["EMP-00001", "EMP-00002", "EMP-00003"], page.Items.Select(u => u.EmployeeCode));
    }

    [Fact]
    public async Task Another_column_without_sortDir_sorts_ascending()
    {
        await using var db = CreateContext();

        var page = await new UserRepository(db).GetPageAsync(
            new UserListQuery { SortBy = UserSortFields.Name, DepartmentId = HrId }, DateTime.UtcNow, CancellationToken.None);

        Assert.Equal([1, 3, 2], page.Items.Select(u => u.Id)); // הרפז, כהן, שמעוני
    }

    [Fact]
    public async Task Sorting_by_employee_code_descending()
    {
        await using var db = CreateContext();

        var page = await new UserRepository(db).GetPageAsync(
            new UserListQuery { SortBy = UserSortFields.EmployeeCode, SortDir = "desc", PageSize = 3 }, DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(["EMP-00130", "EMP-00129", "EMP-00128"], page.Items.Select(u => u.EmployeeCode));
    }

    [Fact]
    public async Task List_items_carry_department_and_ordered_roles()
    {
        await using var db = CreateContext();

        var page = await new UserRepository(db).GetPageAsync(new UserListQuery { Search = "הרפז" }, DateTime.UtcNow, CancellationToken.None);

        var yael = Assert.Single(page.Items);
        Assert.Equal("משאבי אנוש", yael.Department.Name);
        Assert.Equal([Role.Employee, Role.Handler, Role.Admin], yael.Roles);
    }

    [Fact]
    public async Task Details_include_the_managed_department()
    {
        await using var db = CreateContext();

        var ron = await new UserRepository(db).GetDetailsAsync(3, DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(HrId, ron?.ManagedDepartment?.Id);
    }

    [Fact]
    public async Task Invitation_status_comes_from_the_newest_job_that_is_not_cancelled()
    {
        var now = DateTime.UtcNow;
        await using (var setup = CreateContext())
        {
            UserInvitation Link(int userId, DateTime sent, DateTime? used = null, DateTime? revoked = null) => new()
            {
                UserId = userId, TokenHash = Guid.NewGuid().ToString("N"), CreatedAt = sent, ExpiresAt = sent.AddHours(72),
                UsedAt = used, RevokedAt = revoked,
            };
            InvitationEmailJob Job(int userId, InvitationJobStatus status, DateTime created, UserInvitation? link = null) => new()
            {
                OrganizationId = OrgA, UserId = userId, Status = status, CreatedAt = created, NextAttemptAt = created,
                CompletedAt = status == InvitationJobStatus.Pending ? null : created, Invitation = link,
            };

            setup.InvitationEmailJobs.AddRange(
                Job(101, InvitationJobStatus.Pending, now.AddMinutes(-1)),
                Job(102, InvitationJobStatus.Succeeded, now.AddHours(-1), Link(102, now.AddHours(-1))),
                Job(103, InvitationJobStatus.Succeeded, now.AddHours(-2), Link(103, now.AddHours(-2), used: now.AddHours(-1))),
                Job(104, InvitationJobStatus.Succeeded, now.AddHours(-80), Link(104, now.AddHours(-80))),
                Job(105, InvitationJobStatus.Failed, now.AddHours(-1)),
                Job(106, InvitationJobStatus.Succeeded, now.AddHours(-3), Link(106, now.AddHours(-3), revoked: now.AddHours(-2))),
                // Sent, then a resend was cancelled before sending: the status still comes from the sent one.
                Job(107, InvitationJobStatus.Succeeded, now.AddHours(-3), Link(107, now.AddHours(-3))),
                Job(107, InvitationJobStatus.Cancelled, now.AddHours(-1)),
                Job(108, InvitationJobStatus.Cancelled, now.AddHours(-1)));
            await setup.SaveChangesAsync();
        }

        await using var db = CreateContext();
        var page = await new UserRepository(db).GetPageAsync(new UserListQuery { PageSize = 100 }, now, CancellationToken.None);
        var details = await new UserRepository(db).GetDetailsAsync(103, now, CancellationToken.None);

        InvitationStatus? StatusOf(int id) => page.Items.Single(u => u.Id == id).Invitation?.Status;
        Assert.Equal(InvitationStatus.Pending, StatusOf(101));
        Assert.Equal(InvitationStatus.Sent, StatusOf(102));
        Assert.Equal(InvitationStatus.Used, StatusOf(103));
        Assert.Equal(InvitationStatus.Expired, StatusOf(104));
        Assert.Equal(InvitationStatus.Failed, StatusOf(105));
        Assert.Equal(InvitationStatus.Revoked, StatusOf(106));
        Assert.Equal(InvitationStatus.Sent, StatusOf(107));
        Assert.Null(StatusOf(108));
        Assert.Null(StatusOf(1)); // never invited
        Assert.Equal(InvitationStatus.Used, details?.Invitation?.Status);
        Assert.Equal(now.AddHours(-1), details?.Invitation?.At);
    }

    [Fact]
    public async Task Email_check_is_global_across_organizations()
    {
        await using var db = CreateContext();
        var repository = new UserRepository(db);

        Assert.True(await repository.EmailExistsAsync("yael@b.example.com", null, CancellationToken.None));
        Assert.False(await repository.EmailExistsAsync("ron@a.example.com", excludeUserId: 3, CancellationToken.None));
    }

    [Fact]
    public async Task Active_admins_are_counted_within_the_organization()
    {
        await using var db = CreateContext();
        var repository = new UserRepository(db);

        Assert.Equal(1, await repository.CountActiveAdminsAsync(null, CancellationToken.None));
        Assert.Equal(0, await repository.CountActiveAdminsAsync(1, CancellationToken.None));
    }

    [Fact]
    public async Task Departments_are_listed_by_name_with_manager_and_counts_within_the_organization()
    {
        await using var db = CreateContext();

        var departments = await new DepartmentRepository(db).GetAllAsync(includeInactive: false, CancellationToken.None);

        Assert.Equal(["משאבי אנוש", "פיתוח"], departments.Select(d => d.Name));
        var hr = departments[0];
        Assert.Equal(3, hr.Manager?.Id);
        Assert.Equal(3, hr.ActiveUserCount);
        Assert.Equal(2, hr.HandlerCount);
        Assert.Equal(30, departments[1].ActiveUserCount); // the inactive Orit is not counted
    }

    private AppDbContext CreateContext(bool signedIn = true)
    {
        var currentUser = new FakeCurrentUser();
        if (signedIn)
        {
            currentUser.SignedInUserId = 1;
            currentUser.SignedInOrganizationId = OrgA;
        }

        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_databaseName).Options;
        return new AppDbContext(options, new OrganizationContext(currentUser));
    }

    private static User NewUser(int id, int org, int departmentId, string firstName, string lastName, string email,
        params Role[] extraRoles) => new()
    {
        Id = id,
        OrganizationId = org,
        DepartmentId = departmentId,
        EmployeeCode = $"EMP-{id:D5}",
        FirstName = firstName,
        LastName = lastName,
        Email = email,
        JobTitle = "J",
        PasswordHash = "h",
        IsActive = true,
        CreatedAt = DateTime.UtcNow.AddDays(-id),
        Roles = new[] { Role.Employee }.Concat(extraRoles).Select(r => new UserRole { UserId = id, Role = r }).ToList(),
    };
}
