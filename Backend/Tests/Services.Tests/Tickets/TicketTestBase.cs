using System.Text.Json;
using System.Text.Json.Serialization;
using Common.Enums;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Repositories.Data;
using Repositories.Implementations;
using Repositories.Interfaces;
using Services.Implementations;
using Services.Implementations.Routing;
using Services.Implementations.Tickets;
using Services.Tests.Auth.Fakes;
using Services.Tests.Fakes;
using Services.Validators;

namespace Services.Tests.Tickets;

/// <summary>
/// The ticket services over real repositories and EF Core InMemory, with two organizations.
/// Organization A: Development (Noa, Ido – employees; Orit – inactive; Ron – its manager; Alon – handler),
/// HR (Liat – handler; Rina – its manager; Avner – executive), category PAYROLL.
/// Organization B: one employee with one ticket. Every operation uses a fresh context, as a new request would.
/// Attachments go to an in-memory <see cref="FakeFileStorage"/> shared by the test.
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
    protected const int LiatId = 3;   // handler in HR
    protected const int OritId = 4;   // inactive
    protected const int RonId = 5;    // DepartmentManager of Development
    protected const int RinaId = 6;   // DepartmentManager of HR (a handling department)
    protected const int AvnerId = 7;  // Executive
    protected const int AlonId = 8;   // handler in Development
    protected const int OrgBUserId = 200;

    protected const int PayrollId = 1;
    protected const int GeneralId = 2;   // the fallback category → HR
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
    protected readonly CapturingLogger<TicketConversationService> ConversationLogger = new();
    protected readonly FakeFileStorage Files = new();

    /// <summary>Replaces the unit of work of the services (e.g. one that fails), given the request's context.</summary>
    protected Func<AppDbContext, IUnitOfWork>? UnitOfWorkFactory { get; set; }

    private readonly string _databaseName = Guid.NewGuid().ToString();
    private int _nextTicketId = 1000;

    protected TicketTestBase()
    {
        using var db = CreateContext(signedInUserId: null);
        db.Organizations.AddRange(
            new Organization { Id = OrgA, Name = "A", IsActive = true },
            new Organization { Id = OrgB, Name = "B", IsActive = true });
        db.Departments.AddRange(
            new Department { Id = DevId, OrganizationId = OrgA, Name = "פיתוח", IsActive = true, ManagerUserId = RonId },
            new Department { Id = HrId, OrganizationId = OrgA, Name = "משאבי אנוש", IsActive = true, ManagerUserId = RinaId },
            new Department { Id = OrgBDeptId, OrganizationId = OrgB, Name = "פיתוח", IsActive = true });
        db.Users.AddRange(
            NewUser(NoaId, OrgA, DevId, "נועה", "ברקוביץ"),
            NewUser(IdoId, OrgA, DevId, "עידו", "שגיא"),
            NewUser(LiatId, OrgA, HrId, "ליאת", "שחר", Role.Handler),
            NewUser(OritId, OrgA, DevId, "אורית", "דנון", isActive: false),
            NewUser(RonId, OrgA, DevId, "רון", "כהנוביץ", Role.DepartmentManager),
            NewUser(RinaId, OrgA, HrId, "רינה", "קפלן", Role.DepartmentManager),
            NewUser(AvnerId, OrgA, HrId, "אבנר", "גולדמן", Role.Executive),
            NewUser(AlonId, OrgA, DevId, "אלון", "ביטון", Role.Handler),
            NewUser(OrgBUserId, OrgB, OrgBDeptId, "משתמש", "בארגון ב"));
        db.Categories.AddRange(
            new Category
            {
                Id = PayrollId, OrganizationId = OrgA, Code = "PAYROLL", Name = "שכר ותלושים", IsActive = true,
                RoutingType = RoutingType.FixedDepartment, HandlingDepartmentId = DevId,
            },
            // The fallback routes to HR: "central HR" of these tests (task 3.3).
            new Category
            {
                Id = GeneralId, OrganizationId = OrgA, Code = "GENERAL", Name = "כללי", IsActive = true, IsFallback = true,
                RoutingType = RoutingType.FixedDepartment, HandlingDepartmentId = HrId,
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
        var routing = RoutingFor(db);
        return new TicketService(new TicketRepository(db), new UserRepository(db), UnitOfWorkFor(db),
            AccessFor(db, currentUser), routing, ClassifierFor(db, routing), Files, Clock, currentUser,
            new CreateTicketRequestValidator(), new AttachmentFilesValidator(), new MyTicketsQueryValidator(), Logger);
    }

    /// <summary>Reference data cache of the test (one per test, like the app's singleton).</summary>
    protected readonly MemoryReferenceDataCache ReferenceData = new(new MemoryCache(new MemoryCacheOptions()));

    protected RoutingService RoutingFor(AppDbContext db) =>
        new(ReferenceData, new CategoryRepository(db), new DepartmentRepository(db), new UserRepository(db));

    protected TicketClassifier ClassifierFor(AppDbContext db, RoutingService routing) =>
        new(routing, ReferenceData, new SlaPolicyRepository(db));

    /// <summary>The conversation service (comments, attachments, history) as in a request of <paramref name="userId"/>.</summary>
    protected TicketConversationService ConversationFor(int userId, int organizationId = OrgA)
    {
        var db = CreateContext(userId, organizationId);
        var currentUser = new FakeCurrentUser { SignedInUserId = userId, SignedInOrganizationId = organizationId };
        return new TicketConversationService(AccessFor(db, currentUser), new TicketRepository(db),
            new TicketConversationRepository(db), new AuditLogRepository(db), UnitOfWorkFor(db), Files, Clock,
            currentUser, new CreateCommentRequestValidator(), new AttachmentFilesValidator(), ConversationLogger);
    }

    protected TicketAccess AccessFor(int userId, int organizationId = OrgA)
    {
        var db = CreateContext(userId, organizationId);
        return AccessFor(db, new FakeCurrentUser { SignedInUserId = userId, SignedInOrganizationId = organizationId });
    }

    private static TicketAccess AccessFor(AppDbContext db, FakeCurrentUser currentUser) =>
        new(new TicketRepository(db), new UserRepository(db), currentUser);

    private IUnitOfWork UnitOfWorkFor(AppDbContext db) => UnitOfWorkFactory?.Invoke(db) ?? new UnitOfWork(db);

    /// <summary>Comments and attachments as the database has them (no query filter).</summary>
    protected List<TicketComment> AllComments()
    {
        using var db = CreateContext(signedInUserId: null);
        return db.TicketComments.Include(c => c.Attachments).AsNoTracking().ToList();
    }

    protected List<TicketAttachment> AllAttachments()
    {
        using var db = CreateContext(signedInUserId: null);
        return db.TicketAttachments.AsNoTracking().ToList();
    }

    protected List<AuditLog> AllAuditLogs()
    {
        using var db = CreateContext(signedInUserId: null);
        return db.AuditLogs.IgnoreQueryFilters().AsNoTracking().ToList();
    }

    /// <summary>Seeds an attachment of the ticket (to <paramref name="commentId"/>, if given) with a stored PDF.</summary>
    protected int AddAttachment(int ticketId, int uploadedBy, int? commentId = null, string fileName = "קובץ.pdf")
    {
        var name = Guid.NewGuid().ToString("N");
        Files.Files[name] = TestFiles.Pdf;
        using var db = CreateContext(signedInUserId: null);
        var attachment = new TicketAttachment
        {
            TicketId = ticketId, CommentId = commentId, OriginalFileName = fileName, StoredFileName = name,
            ContentType = "application/pdf", SizeBytes = TestFiles.Pdf.Length, UploadedByUserId = uploadedBy,
            CreatedAt = Now.AddMinutes(-30),
        };
        db.TicketAttachments.Add(attachment);
        db.SaveChanges();
        return attachment.Id;
    }

    /// <summary>Seeds a comment on the ticket and returns its id.</summary>
    protected int AddComment(int ticketId, int authorId, string body, bool isInternal = false, DateTime? at = null)
    {
        using var db = CreateContext(signedInUserId: null);
        var comment = new TicketComment
        {
            TicketId = ticketId, AuthorUserId = authorId, Body = body, IsInternal = isInternal,
            CreatedAt = at ?? Now.AddMinutes(-40),
        };
        db.TicketComments.Add(comment);
        db.SaveChanges();
        return comment.Id;
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
