using System.Text.Json;
using System.Text.Json.Serialization;
using Common.Enums;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Repositories.Data;
using Repositories.Implementations;
using Services.Implementations;
using Services.Tests.Auth.Fakes;
using Services.Tests.Fakes;
using Services.Validators;

namespace Services.Tests.Tickets;

/// <summary>
/// The ticket service over real repositories and EF Core InMemory, with two organizations.
/// Organization A: Development (Noa, Ido – employees; Orit – inactive), HR (Liat – handler), category PAYROLL.
/// Organization B: one employee with one ticket. Every operation uses a fresh context, as a new request would.
/// </summary>
public abstract class TicketTestBase
{
    protected const int OrgA = 1;
    protected const int OrgB = 2;
    protected const int DevId = 10;
    protected const int HrId = 11;
    protected const int OrgBDeptId = 20;

    protected const int NoaId = 1;
    protected const int IdoId = 2;
    protected const int LiatId = 3;   // handler
    protected const int OritId = 4;   // inactive
    protected const int OrgBUserId = 200;

    protected const int PayrollId = 1;
    protected const int OrgBTicketId = 9000;

    /// <summary>The access-token department claim of <see cref="FakeCurrentUser"/> – deliberately not a real department.</summary>
    protected const int ClaimDepartmentId = 1;

    /// <summary>12/11/2026 11:00 Israel time. The month started on 31/10/2026 22:00 UTC.</summary>
    protected static readonly DateTime Now = new(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>The API's JSON settings (ASP.NET Core web defaults + enums as names, see AddApi).</summary>
    protected static readonly JsonSerializerOptions ApiJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    protected readonly FakeDateTimeProvider Clock = new(Now);
    protected readonly CapturingLogger<TicketService> Logger = new();

    private readonly string _databaseName = Guid.NewGuid().ToString();
    private int _nextTicketId = 1000;

    protected TicketTestBase()
    {
        using var db = CreateContext(signedInUserId: null);
        db.Organizations.AddRange(
            new Organization { Id = OrgA, Name = "A", IsActive = true },
            new Organization { Id = OrgB, Name = "B", IsActive = true });
        db.Departments.AddRange(
            new Department { Id = DevId, OrganizationId = OrgA, Name = "פיתוח", IsActive = true },
            new Department { Id = HrId, OrganizationId = OrgA, Name = "משאבי אנוש", IsActive = true },
            new Department { Id = OrgBDeptId, OrganizationId = OrgB, Name = "פיתוח", IsActive = true });
        db.Users.AddRange(
            NewUser(NoaId, OrgA, DevId, "נועה", "ברקוביץ"),
            NewUser(IdoId, OrgA, DevId, "עידו", "שגיא"),
            NewUser(LiatId, OrgA, HrId, "ליאת", "שחר", Role.Handler),
            NewUser(OritId, OrgA, DevId, "אורית", "דנון", isActive: false),
            NewUser(OrgBUserId, OrgB, OrgBDeptId, "משתמש", "בארגון ב"));
        db.Categories.Add(new Category
        {
            Id = PayrollId, OrganizationId = OrgA, Code = "PAYROLL", Name = "שכר ותלושים", IsActive = true,
        });
        db.Tickets.Add(NewTicket(OrgBTicketId, OrgB, OrgBUserId, OrgBDeptId, TicketStatus.New, Now.AddDays(-1)));
        db.SaveChanges();
    }

    /// <summary>A fresh context of a request by <paramref name="signedInUserId"/> (null: nobody, filters return nothing).</summary>
    protected AppDbContext CreateContext(int? signedInUserId, int organizationId = OrgA)
    {
        var currentUser = new FakeCurrentUser { SignedInUserId = signedInUserId, SignedInOrganizationId = organizationId };
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_databaseName).Options;
        return new AppDbContext(options, new OrganizationContext(currentUser));
    }

    /// <summary>The service as in a request of <paramref name="userId"/>, over its own fresh context.</summary>
    protected TicketService ServiceFor(int userId, int organizationId = OrgA)
    {
        var db = CreateContext(userId, organizationId);
        var currentUser = new FakeCurrentUser { SignedInUserId = userId, SignedInOrganizationId = organizationId };
        return new TicketService(new TicketRepository(db), new UserRepository(db), new UnitOfWork(db), Clock, currentUser,
            new CreateTicketRequestValidator(), new MyTicketsQueryValidator(), Logger);
    }

    /// <summary>Reads tickets as the database has them (no query filter: nobody signed in, so IgnoreQueryFilters).</summary>
    protected List<Ticket> AllTickets()
    {
        using var db = CreateContext(signedInUserId: null);
        return db.Tickets.IgnoreQueryFilters().Include(t => t.StatusHistory).AsNoTracking().ToList();
    }

    /// <summary>Seeds a ticket of organization A and returns its id. <paramref name="configure"/> sets anything else.</summary>
    protected int AddTicket(int requesterId, TicketStatus status, DateTime createdAt, string? subject = "נושא",
        string description = "תיאור של הפנייה", Action<Ticket>? configure = null)
    {
        using var db = CreateContext(signedInUserId: null);
        var ticket = NewTicket(_nextTicketId++, OrgA, requesterId, DevId, status, createdAt, subject, description);
        configure?.Invoke(ticket);
        db.Tickets.Add(ticket);
        db.SaveChanges();
        return ticket.Id;
    }

    private static Ticket NewTicket(int id, int org, int requesterId, int departmentId, TicketStatus status,
        DateTime createdAt, string? subject = "נושא", string description = "תיאור של הפנייה") => new()
    {
        Id = id,
        OrganizationId = org,
        RequesterId = requesterId,
        RequesterDepartmentId = departmentId,
        Subject = subject,
        Description = description,
        Status = status,
        AiStatus = AiStatus.Completed,
        CreatedAt = createdAt,
        UpdatedAt = createdAt,
        RowVersion = [1],
    };

    private static User NewUser(int id, int org, int departmentId, string firstName, string lastName,
        Role? extraRole = null, bool isActive = true) => new()
    {
        Id = id,
        OrganizationId = org,
        DepartmentId = departmentId,
        EmployeeCode = $"EMP-{id:D5}",
        FirstName = firstName,
        LastName = lastName,
        Email = $"user{id}@example.com",
        JobTitle = "J",
        PasswordHash = "h",
        IsActive = isActive,
        CreatedAt = Now.AddDays(-100),
        Roles = (extraRole is { } role ? new[] { Role.Employee, role } : [Role.Employee])
            .Select(r => new UserRole { UserId = id, Role = r }).ToList(),
    };
}
