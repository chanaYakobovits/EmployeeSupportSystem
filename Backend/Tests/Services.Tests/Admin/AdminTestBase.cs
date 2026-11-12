using Common.Enums;
using Common.Models;
using Microsoft.Extensions.Caching.Memory;
using Services.Implementations;
using Services.Tests.Admin.Fakes;
using Services.Tests.Auth.Fakes;
using Services.Tests.Fakes;
using Services.Validators;

namespace Services.Tests.Admin;

/// <summary>
/// UserService and DepartmentService over hand-written fakes, with a small organization (ids as in the demo seed):
/// Management (CEO Avner, Executive), Development (manager Ron, Noa, handler Liat), HR (manager + admin Yael),
/// and an inactive department. The signed-in admin is Yael.
/// </summary>
public abstract class AdminTestBase
{
    protected const string Ip = "10.0.0.1";
    protected const int OrgId = 1;

    protected const int ManagementId = 1;
    protected const int DevelopmentId = 2;
    protected const int HrId = 6;
    protected const int InactiveDepartmentId = 7;

    protected const int AvnerId = 1;   // Executive, manages Management (without DepartmentManager)
    protected const int RonId = 3;     // DepartmentManager of Development
    protected const int NoaId = 4;     // Employee
    protected const int LiatId = 10;   // Handler
    protected const int YaelId = 23;   // Admin + Handler + DepartmentManager of HR – the signed-in admin
    protected const int OritId = 12;   // inactive, in the inactive department

    protected static readonly DateTime Now = new(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);

    protected readonly FakeUserRepository Users = new();
    protected readonly FakeDepartmentRepository Departments;
    protected readonly FakeRefreshTokenRepository RefreshTokens = new();
    protected readonly FakeTicketRepository Tickets = new();
    protected readonly FakeAuditLogRepository AuditLog = new();
    protected readonly FakeUnitOfWork UnitOfWork = new();
    protected readonly FakePasswordHasher Hasher = new();
    protected readonly FakeDateTimeProvider Clock = new(Now);
    protected readonly FakeCurrentUser CurrentUser = new() { SignedInUserId = YaelId, SignedInRoles = [Role.Employee, Role.Admin] };
    protected readonly MemoryCache Cache = new(new MemoryCacheOptions());
    protected readonly CapturingLogger<UserService> UserLogger = new();
    protected readonly UserService UserService;
    protected readonly DepartmentService DepartmentService;

    protected AdminTestBase()
    {
        Departments = new FakeDepartmentRepository(Users);
        AddDepartment(ManagementId, "הנהלה", AvnerId);
        AddDepartment(DevelopmentId, "פיתוח", RonId);
        AddDepartment(HrId, "משאבי אנוש מרכזי", YaelId);
        AddDepartment(InactiveDepartmentId, "מחלקה שנסגרה", managerId: null, isActive: false);

        AddUser(AvnerId, "avner.goldman", ManagementId, Role.Executive);
        AddUser(RonId, "ron.kahanovich", DevelopmentId, Role.DepartmentManager);
        AddUser(NoaId, "noa.berkovich", DevelopmentId);
        AddUser(LiatId, "liat.shahar", DevelopmentId, Role.Handler);
        AddUser(YaelId, "yael.harpaz", HrId, Role.Admin, Role.Handler, Role.DepartmentManager);
        AddUser(OritId, "orit.danon", InactiveDepartmentId).IsActive = false;

        UserService = new UserService(Users, Departments, RefreshTokens, Tickets, AuditLog, UnitOfWork, Hasher, Clock,
            CurrentUser, new UserListQueryValidator(), new CreateUserRequestValidator(), new UpdateUserRequestValidator(),
            new SetUserActiveRequestValidator(), UserLogger);

        DepartmentService = new DepartmentService(Departments, Users, AuditLog, UnitOfWork, Clock, CurrentUser, Cache,
            new CreateDepartmentRequestValidator(), new UpdateDepartmentRequestValidator(),
            new CapturingLogger<DepartmentService>());
    }

    protected User UserById(int id) => Users.Users.Single(u => u.Id == id);

    protected static IReadOnlyList<Role> RolesOf(User user) => user.Roles.Select(r => r.Role).Order().ToList();

    /// <summary>Simulates a stale access token: the signed-in admin was demoted in the database meanwhile.</summary>
    protected void SignInAsDemotedAdmin()
    {
        var demoted = AddUser(99, "former.admin", HrId);
        CurrentUser.SignedInUserId = demoted.Id;
    }

    protected User AddUser(int id, string emailLocalPart, int departmentId, params Role[] extraRoles)
    {
        var user = new User
        {
            Id = id,
            OrganizationId = OrgId,
            DepartmentId = departmentId,
            Department = Departments.Departments[departmentId],
            EmployeeCode = $"EMP-{id:D5}",
            FirstName = "First" + id,
            LastName = "Last" + id,
            Email = emailLocalPart + "@example.com",
            JobTitle = "Job",
            PasswordHash = Hasher.Hash("Demo1234"),
            IsActive = true,
            CreatedAt = Now.AddDays(-100),
            Roles = new[] { Role.Employee }.Concat(extraRoles).Select(r => new UserRole { UserId = id, Role = r }).ToList(),
        };
        Users.Users.Add(user);
        return user;
    }

    protected void AddActiveRefreshTokens(int userId, int count)
    {
        for (var i = 0; i < count; i++)
        {
            RefreshTokens.Add(new RefreshToken
            {
                UserId = userId,
                TokenHash = $"hash-{userId}-{i}",
                CreatedAt = Now.AddHours(-1),
                ExpiresAt = Now.AddDays(6),
            });
        }
    }

    private void AddDepartment(int id, string name, int? managerId, bool isActive = true) =>
        Departments.Departments[id] = new Department
        {
            Id = id, OrganizationId = OrgId, Name = name, ManagerUserId = managerId, IsActive = isActive,
        };
}
