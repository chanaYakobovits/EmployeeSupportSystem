using Common.Enums;
using Common.Models;
using Common.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Repositories.Data;
using Repositories.Implementations;
using Services.Implementations;
using Services.Implementations.Ai;
using Services.Tests.Ai.Fakes;
using Services.Tests.Auth.Fakes;
using Services.Tests.Fakes;
using Services.Tests.Tickets;

namespace Services.Tests.Ai;

/// <summary>
/// The AI worker's processor (task 4.3) over the ticket test data: PAYROLL → Development (handler Alon), GENERAL (the
/// fallback) → HR (handler Liat). Each job runs in a fresh context bound to organization A, as the worker's scope.
/// The claim itself is SQL (UPDATE ... OUTPUT with READPAST) and does not run on InMemory: <see cref="Claim"/> does what
/// it does to one row; the statement is checked against SQL Server separately.
/// </summary>
public abstract class AiTestBase : TicketTestBase
{
    protected readonly AiOptions Ai = new()
    {
        Enabled = true,
        ApiKey = "test-key",
        RetryDelaysSeconds = [.. AiOptions.DefaultRetryDelaysSeconds],
    };

    protected readonly FakeAiClient AiClient = new();
    protected readonly CapturingLogger<AiAnalysisProcessor> ProcessorLogger = new();
    protected readonly CapturingLogger<AiCircuitBreaker> BreakerLogger = new();
    protected readonly AiCircuitBreaker Breaker;

    protected AiTestBase()
    {
        Breaker = new AiCircuitBreaker(Clock, Options.Create(Ai), BreakerLogger);
    }

    /// <summary>A context bound to organization A without a user – the worker's scope.</summary>
    protected AppDbContext SystemContext()
    {
        var context = new OrganizationContext(new FakeCurrentUser { SignedInUserId = null });
        context.Enter(OrgA);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(DatabaseName).Options;
        return new AppDbContext(options, context);
    }

    protected AiAnalysisProcessor Processor()
    {
        var db = SystemContext();
        var routing = RoutingFor(db);
        return new AiAnalysisProcessor(new AiAnalysisJobRepository(db), new TicketRepository(db), InputBuilderFor(db),
            routing, ClassifierFor(db, routing), AiClient, Breaker, UnitOfWorkFor(db), Clock, Options.Create(Ai),
            ProcessorLogger);
    }

    protected AiInputBuilder InputBuilderFor(AppDbContext db) =>
        new(new UserRepository(db), new CategoryRepository(db), ReferenceData, new PiiRedactor());

    /// <summary>A ticket as ticket creation leaves it with the AI available: New, AiStatus Pending, one Pending job.</summary>
    protected (int TicketId, int JobId) AddQueuedTicket(DateTime? createdAt = null, string? subject = "תלוש שכר",
        string description = "לא קיבלתי את תלוש השכר של החודש", int requesterId = NoaId)
    {
        var at = createdAt ?? Now;
        var ticketId = AddTicket(requesterId, TicketStatus.New, at, subject, description, t =>
        {
            t.AiStatus = AiStatus.Pending;
            t.AiAnalysisJobs.Add(new AiAnalysisJob
            {
                OrganizationId = OrgA, Status = AiJobStatus.Pending, NextAttemptAt = at, CreatedAt = at,
            });
        });

        using var db = SystemContext();
        return (ticketId, db.AiAnalysisJobs.Single(j => j.TicketId == ticketId).Id);
    }

    /// <summary>
    /// What the claim statement does to one row: a due Pending job, or a Processing one whose lock expired →
    /// Processing, Attempts + 1, LockedUntil. False if the job is not claimable now.
    /// </summary>
    protected bool Claim(int jobId)
    {
        using var db = SystemContext();
        var job = db.AiAnalysisJobs.Single(j => j.Id == jobId);
        var now = Clock.UtcNow;
        var due = (job.Status == AiJobStatus.Pending && job.NextAttemptAt <= now)
                  || (job.Status == AiJobStatus.Processing && job.LockedUntil < now);
        if (!due)
        {
            return false;
        }

        job.Status = AiJobStatus.Processing;
        job.Attempts++;
        job.LockedUntil = now + Processor().LockDuration;
        db.SaveChanges();
        return true;
    }

    /// <summary>Claims the job (it must be due) and processes it, as one worker round.</summary>
    protected async Task RunAsync(int jobId)
    {
        Assert.True(Claim(jobId), "The job is not due.");
        await Processor().ProcessAsync(jobId, CancellationToken.None);
    }

    protected Ticket TicketRow(int ticketId)
    {
        using var db = SystemContext();
        return db.Tickets.AsNoTracking().Include(t => t.StatusHistory).Single(t => t.Id == ticketId);
    }

    protected AiAnalysisJob JobRow(int jobId)
    {
        using var db = SystemContext();
        return db.AiAnalysisJobs.AsNoTracking().Single(j => j.Id == jobId);
    }

    protected TicketAiAnalysis? AnalysisRow(int ticketId)
    {
        using var db = SystemContext();
        return db.TicketAiAnalyses.AsNoTracking().SingleOrDefault(a => a.TicketId == ticketId);
    }

    /// <summary>Changes a ticket directly in the database, as another request would.</summary>
    protected void ChangeTicket(int ticketId, Action<Ticket> change)
    {
        using var db = SystemContext();
        change(db.Tickets.Single(t => t.Id == ticketId));
        db.SaveChanges();
    }
}
