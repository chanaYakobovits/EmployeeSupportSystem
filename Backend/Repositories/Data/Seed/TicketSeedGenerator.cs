using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Common.Constants;
using Common.Enums;
using Common.Models;
using static Repositories.Data.Seed.ReferenceSeedData;

namespace Repositories.Data.Seed;

/// <summary>The already-saved organization data the ticket generator builds on.</summary>
internal sealed class SeedOrganization
{
    public required IReadOnlyDictionary<DepartmentKey, Department> Departments { get; init; }
    public required IReadOnlyList<SeededUser> Users { get; init; }
    public required IReadOnlyDictionary<string, Category> Categories { get; init; }
    public required int OrganizationId { get; init; }
}

internal sealed record SeededUser(User User, PersonSeed Person)
{
    public bool IsActiveHandler => Person.IsActive && Person.ExtraRoles.Contains(Role.Handler);
}

/// <summary>
/// Generates demo tickets by simulating each ticket's life cycle, so every ticket is consistent:
/// routing follows spec 3.5, DueAt follows the SLA (3.4), dates are ordered, the status history uses only
/// allowed transitions (3.3), and resolved tickets carry a resolution comment.
/// Fully deterministic for a given Random seed and anchor time.
/// </summary>
internal sealed class TicketSeedGenerator(SeedOrganization organization, Random random, DateTime now)
{
    private const string AiModel = "gpt-4o-mini";
    private const string PromptVersion = "v1";
    private const int TargetBreaches = 12;
    private const int ProcessingNewTickets = 2;

    private static readonly string[] AiErrorCodes = ["Timeout", "RateLimited", "InvalidResponse"];
    private static readonly TimeSpan AutoCloseAfter = TimeSpan.FromDays(7);
    private static readonly TimeSpan SeedWindow = TimeSpan.FromDays(60);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private static readonly (TicketStatus Status, int Count)[] StatusCounts =
    [
        (TicketStatus.New, 6),
        (TicketStatus.Assigned, 30),
        (TicketStatus.InProgress, 35),
        (TicketStatus.WaitingForEmployee, 14),
        (TicketStatus.Resolved, 20),
        (TicketStatus.Closed, 85),
        (TicketStatus.Cancelled, 10)
    ];

    // Category of the ticket content. Fallback tickets end up in GENERAL regardless of their content.
    private static readonly (string Code, int Count)[] ContentCategoryCounts =
    [
        (Payroll, 32), (Attendance, 32), (Leave, 30), (Equipment, 38),
        (Admin, 28), (Benefits, 16), (Training, 14), (General, 10)
    ];

    private enum Source { Ai, AiFailedFallback, LowConfidenceFallback, Manual }

    private enum Step { ManualClassify, Start, Wait, Resume, Resolve, Reopen, CloseByEmployee, CloseBySystem, Cancel }

    private readonly Dictionary<int, int> _handlerLoad = [];
    private int _breachesLeft = TargetBreaches;
    private int _processingLeft = ProcessingNewTickets;

    private Department HumanResources => organization.Departments[DepartmentKey.HumanResources];

    public List<Ticket> Generate()
    {
        var statuses = Shuffle(StatusCounts.SelectMany(s => Enumerable.Repeat(s.Status, s.Count)).ToList());
        var contentCodes = Shuffle(ContentCategoryCounts.SelectMany(c => Enumerable.Repeat(c.Code, c.Count)).ToList());

        var classifiedCount = statuses.Count(s => s != TicketStatus.New);
        var sources = Shuffle(Enumerable.Repeat(Source.AiFailedFallback, 11)
            .Concat(Enumerable.Repeat(Source.LowConfidenceFallback, 11))
            .Concat(Enumerable.Repeat(Source.Manual, 24))
            .Concat(Enumerable.Repeat(Source.Ai, classifiedCount - 46))
            .ToList());

        var tickets = new List<Ticket>(statuses.Count);
        var sourceIndex = 0;

        for (var i = 0; i < statuses.Count; i++)
        {
            var template = PickTemplate(contentCodes[i]);

            if (statuses[i] == TicketStatus.New)
            {
                tickets.Add(BuildNew(template));
                continue;
            }

            var source = sources[sourceIndex++];

            // A handler never "manually classifies" into the fallback category.
            if (source == Source.Manual && template.CategoryCode == General)
            {
                source = Source.Ai;
            }

            tickets.Add(BuildClassified(statuses[i], template, source));
        }

        // Ticket numbers grow with creation time, as in real life.
        return tickets.OrderBy(t => t.CreatedAt).ToList();
    }

    private Ticket BuildNew(TicketTemplate template)
    {
        var created = now - Minutes(1, 9);
        var processing = _processingLeft-- > 0;
        var ticket = CreateTicket(template, PickRequester(TicketStatus.New, created), created);

        ticket.Status = TicketStatus.New;
        ticket.AiStatus = processing ? AiStatus.Processing : AiStatus.Pending;
        ticket.UpdatedAt = created;
        AddHistory(ticket, null, TicketStatus.New, null, created);

        ticket.AiAnalysisJobs.Add(new AiAnalysisJob
        {
            Status = processing ? AiJobStatus.Processing : AiJobStatus.Pending,
            Attempts = processing ? 1 : 0,
            NextAttemptAt = created,
            LockedUntil = processing ? now.AddMinutes(2) : null,
            CreatedAt = created
        });

        return ticket;
    }

    private Ticket BuildClassified(TicketStatus finalStatus, TicketTemplate template, Source source)
    {
        var isFallback = source is Source.AiFailedFallback or Source.LowConfidenceFallback;
        var finalUrgency = isFallback ? (Urgency?)null : template.Urgency;
        var slaHours = SlaDefaults.Hours[finalUrgency ?? Urgency.Medium]; // spec 3.4: no urgency yet -> Medium target

        var steps = PlanSteps(finalStatus, source, out var hasPause, out var wasReopened);
        var gaps = steps.Select(StepGap).ToList();

        // AI answers within a few minutes; a failed analysis falls back after the last retry (spec 4.3).
        var classifyDelay = source is Source.Ai or Source.LowConfidenceFallback
            ? Minutes(1, 3)
            : TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(random.Next(0, 60));

        var isOpen = finalStatus is TicketStatus.Assigned or TicketStatus.InProgress or TicketStatus.WaitingForEmployee;
        var breach = finalStatus is TicketStatus.Assigned or TicketStatus.InProgress
                     && !hasPause && !wasReopened && _breachesLeft > 0 && random.NextDouble() < 0.3;
        if (breach)
        {
            _breachesLeft--;
        }

        DateTime created;
        DateTime? lastEvent = null;

        if (isOpen)
        {
            created = finalStatus == TicketStatus.WaitingForEmployee
                ? now - Hours(4, 15 * 24)
                : breach
                    ? now - TimeSpan.FromHours(slaHours) - Hours(1, 72)
                    : now - TimeSpan.FromHours(Math.Max(0.5, slaHours * (0.15 + 0.7 * random.NextDouble())));

            // Squeeze the steps into the time that has passed since the ticket was opened.
            var available = now - created - classifyDelay - TimeSpan.FromMinutes(5);
            var total = Sum(gaps);
            if (total > available)
            {
                var factor = available / total;
                gaps = gaps.Select(g => g * factor).ToList();
            }
        }
        else
        {
            var total = classifyDelay + Sum(gaps);
            var end = finalStatus == TicketStatus.Resolved
                ? now - Hours(2, 150) // resolved within 7 days, otherwise it would have been auto-closed
                : now - Hours(1, 50 * 24);

            var earliestEnd = now - SeedWindow + total + TimeSpan.FromHours(1);
            end = end < earliestEnd ? earliestEnd : end;
            created = end - total;
            lastEvent = end;
        }

        var requester = PickRequester(finalStatus, created, lastEvent);
        var ticket = CreateTicket(template, requester, created);
        AddHistory(ticket, null, TicketStatus.New, null, created);

        // --- Initial classification and routing (AI or fallback), status New -> Assigned ---
        var time = created + classifyDelay;
        var initialCategory = source == Source.Ai ? organization.Categories[template.CategoryCode] : organization.Categories[General];
        var (initialDepartment, initialHandler) = Route(initialCategory, requester.User);

        ticket.CategoryId = initialCategory.Id;
        ticket.Urgency = source == Source.Ai ? template.Urgency : null;
        ticket.ClassificationSource = source == Source.Ai ? ClassificationSource.Ai : ClassificationSource.Fallback;
        ticket.AssignedDepartmentId = initialDepartment.Id;
        ticket.AssignedToUserId = initialHandler?.Id;
        AddAiArtifacts(ticket, template, source, created, time);
        AddHistory(ticket, TicketStatus.New, TicketStatus.Assigned, null, time);

        var current = TicketStatus.Assigned;
        var pausedTotal = TimeSpan.Zero;
        DateTime? resolvedAt = null;
        DateTime? closedAt = null;
        User? routedHandler = initialHandler;

        for (var i = 0; i < steps.Count; i++)
        {
            time += gaps[i];
            var handlerId = ticket.AssignedToUserId;

            switch (steps[i])
            {
                case Step.ManualClassify:
                {
                    // A central HR handler classifies the fallback ticket and transfers it to the right department queue.
                    var category = organization.Categories[template.CategoryCode];
                    var (department, handler) = Route(category, requester.User);
                    ticket.CategoryId = category.Id;
                    ticket.Urgency = template.Urgency;
                    ticket.ClassificationSource = ClassificationSource.Manual;
                    ticket.AssignedDepartmentId = department.Id;
                    ticket.AssignedToUserId = null;
                    routedHandler = handler;

                    AddComment(ticket, handlerId ?? requester.User.Id,
                        $"סיווג ידני: הפנייה סווגה לקטגוריה \"{category.Name}\" והועברה ל{department.Name}.",
                        time, isInternal: true);
                    break;
                }

                case Step.Start:
                    // The handler who takes a queued ticket becomes its handler (spec 3.3).
                    ticket.AssignedToUserId ??= routedHandler?.Id;
                    AddHistory(ticket, current, TicketStatus.InProgress, ticket.AssignedToUserId, time);
                    current = TicketStatus.InProgress;
                    if (random.NextDouble() < 0.25 && ticket.AssignedToUserId is int noteAuthor)
                    {
                        AddComment(ticket, noteAuthor, Pick(TicketTemplates.InternalNotes), time, isInternal: true);
                    }
                    break;

                case Step.Wait:
                    AddComment(ticket, ticket.AssignedToUserId!.Value,
                        template.MissingInfo.Length > 0 ? template.MissingInfo[0] : TicketTemplates.DefaultInfoRequest, time);
                    AddHistory(ticket, current, TicketStatus.WaitingForEmployee, ticket.AssignedToUserId, time);
                    current = TicketStatus.WaitingForEmployee;
                    break;

                case Step.Resume:
                    // The employee's reply automatically resumes handling; the SLA clock was paused meanwhile.
                    pausedTotal += gaps[i];
                    AddComment(ticket, requester.User.Id, Pick(TicketTemplates.EmployeeReplies), time);
                    AddHistory(ticket, current, TicketStatus.InProgress, null, time);
                    current = TicketStatus.InProgress;
                    break;

                case Step.Resolve:
                    AddComment(ticket, ticket.AssignedToUserId!.Value,
                        resolvedAt is null ? template.Solution : "בדקנו שוב. " + template.Solution,
                        time, isResolution: true);
                    AddHistory(ticket, current, TicketStatus.Resolved, ticket.AssignedToUserId, time);
                    current = TicketStatus.Resolved;
                    resolvedAt = time;
                    break;

                case Step.Reopen:
                {
                    var reason = Pick(TicketTemplates.ReopenReasons);
                    AddComment(ticket, requester.User.Id, reason, time);
                    AddHistory(ticket, current, TicketStatus.InProgress, requester.User.Id, time, "פתיחה מחדש");
                    current = TicketStatus.InProgress;
                    resolvedAt = null;
                    break;
                }

                case Step.CloseByEmployee:
                    AddHistory(ticket, current, TicketStatus.Closed, requester.User.Id, time, "העובד אישר את הפתרון");
                    current = TicketStatus.Closed;
                    closedAt = time;
                    break;

                case Step.CloseBySystem:
                    AddHistory(ticket, current, TicketStatus.Closed, null, time, "סגירה אוטומטית 7 ימים אחרי הפתרון");
                    current = TicketStatus.Closed;
                    closedAt = time;
                    break;

                case Step.Cancel:
                    AddHistory(ticket, current, TicketStatus.Cancelled, requester.User.Id, time, "בוטלה על ידי העובד");
                    current = TicketStatus.Cancelled;
                    break;
            }
        }

        ticket.Status = current;
        ticket.ResolvedAt = resolvedAt;
        ticket.ClosedAt = closedAt;
        ticket.UpdatedAt = time;
        ticket.DueAt = created + TimeSpan.FromHours(SlaDefaults.Hours[ticket.Urgency ?? Urgency.Medium]) + pausedTotal;

        if (ticket.AssignedToUserId is int assignee)
        {
            _handlerLoad[assignee] = _handlerLoad.GetValueOrDefault(assignee) + 1;
        }

        return ticket;
    }

    private List<Step> PlanSteps(TicketStatus finalStatus, Source source, out bool hasPause, out bool wasReopened)
    {
        var steps = new List<Step>();
        if (source == Source.Manual)
        {
            steps.Add(Step.ManualClassify);
        }

        hasPause = false;
        wasReopened = false;

        switch (finalStatus)
        {
            case TicketStatus.Cancelled:
                steps.Add(Step.Cancel);
                break;

            case TicketStatus.WaitingForEmployee:
                steps.AddRange([Step.Start, Step.Wait]);
                break;

            case TicketStatus.InProgress:
                steps.Add(Step.Start);
                if (random.NextDouble() < 0.12)
                {
                    wasReopened = true;
                    steps.AddRange([Step.Resolve, Step.Reopen]);
                }
                else if (random.NextDouble() < 0.25)
                {
                    hasPause = true;
                    steps.AddRange([Step.Wait, Step.Resume]);
                }
                break;

            case TicketStatus.Resolved:
            case TicketStatus.Closed:
                steps.Add(Step.Start);
                if (random.NextDouble() < 0.25)
                {
                    hasPause = true;
                    steps.AddRange([Step.Wait, Step.Resume]);
                }

                steps.Add(Step.Resolve);
                if (random.NextDouble() < (finalStatus == TicketStatus.Resolved ? 0.15 : 0.10))
                {
                    wasReopened = true;
                    steps.AddRange([Step.Reopen, Step.Resolve]);
                }

                if (finalStatus == TicketStatus.Closed)
                {
                    steps.Add(random.NextDouble() < 0.3 ? Step.CloseBySystem : Step.CloseByEmployee);
                }
                break;
        }

        return steps;
    }

    private TimeSpan StepGap(Step step) => step switch
    {
        Step.ManualClassify => Hours(0.25, 4),
        Step.Start => Hours(0.5, 24),
        Step.Wait => Hours(1, 48),
        Step.Resume => Hours(2, 48),
        Step.Resolve => Hours(1, 72),
        Step.Reopen => Hours(2, 120),          // reopening is allowed only within 7 days
        Step.CloseByEmployee => Hours(1, 96),  // before the 7-day auto-close
        Step.CloseBySystem => AutoCloseAfter,
        Step.Cancel => Hours(0.2, 30),
        _ => throw new ArgumentOutOfRangeException(nameof(step))
    };

    /// <summary>Routing rules of spec 3.5, with simple load balancing between the department's handlers.</summary>
    private (Department Department, User? Handler) Route(Category category, User requester)
    {
        if (category.RoutingType == RoutingType.RequesterDepartmentHr)
        {
            var departmentHr = organization.Users
                .Where(u => u.Person.IsDepartmentHr && u.IsActiveHandler
                            && u.User.DepartmentId == requester.DepartmentId && u.User.Id != requester.Id)
                .Select(u => u.User)
                .FirstOrDefault();

            if (departmentHr is not null)
            {
                return (organization.Departments.Values.Single(d => d.Id == requester.DepartmentId), departmentHr);
            }

            // No departmental HR -> the central HR queue.
            return (HumanResources, PickHandler(HumanResources.Id, requester));
        }

        var department = organization.Departments.Values.Single(d => d.Id == category.HandlingDepartmentId);
        var handler = PickHandler(department.Id, requester);
        return handler is not null ? (department, handler) : (HumanResources, PickHandler(HumanResources.Id, requester));
    }

    private User? PickHandler(int departmentId, User requester) =>
        organization.Users
            .Where(u => u.IsActiveHandler && !u.Person.IsDepartmentHr
                        && u.User.DepartmentId == departmentId && u.User.Id != requester.Id)
            .Select(u => u.User)
            .OrderBy(u => _handlerLoad.GetValueOrDefault(u.Id))
            .ThenBy(u => u.Id)
            .FirstOrDefault();

    private SeededUser PickRequester(TicketStatus status, DateTime created, DateTime? lastEvent = null)
    {
        // The employee who left has only old, fully closed tickets from before leaving.
        if (status == TicketStatus.Closed && lastEvent < now.AddDays(-40) && random.NextDouble() < 0.3)
        {
            return organization.Users.First(u => !u.Person.IsActive);
        }

        // The user who has never logged in (must change password) has no tickets.
        var pool = organization.Users.Where(u => u.Person.IsActive && !u.Person.MustChangePassword).ToList();
        return pool[random.Next(pool.Count)];
    }

    private Ticket CreateTicket(TicketTemplate template, SeededUser requester, DateTime created) => new()
    {
        OrganizationId = organization.OrganizationId,
        RequesterId = requester.User.Id,
        RequesterDepartmentId = requester.User.DepartmentId, // snapshot at creation time
        Subject = template.Subject,
        Description = template.Description,
        CreatedAt = created,
        UpdatedAt = created
    };

    private void AddAiArtifacts(Ticket ticket, TicketTemplate template, Source source, DateTime created, DateTime classifiedAt)
    {
        var job = new AiAnalysisJob { CreatedAt = created, CompletedAt = classifiedAt };

        if (source is Source.Ai or Source.LowConfidenceFallback)
        {
            var retried = random.NextDouble() < 0.15;
            job.Status = AiJobStatus.Succeeded;
            job.Attempts = retried ? 2 : 1;
            job.NextAttemptAt = retried ? created.AddSeconds(30) : created;
            job.LastErrorCode = retried ? Pick(AiErrorCodes) : null;

            var confidence = source == Source.Ai
                ? 0.62 + 0.36 * random.NextDouble()
                : 0.30 + 0.28 * random.NextDouble(); // below MinConfidence 0.6 -> GENERAL, kept as a recommendation

            ticket.AiStatus = AiStatus.Completed;
            ticket.AiAnalysis = new TicketAiAnalysis
            {
                Model = AiModel,
                PromptVersion = PromptVersion,
                CategoryCode = template.CategoryCode,
                Urgency = template.Urgency,
                Confidence = Math.Round((decimal)confidence, 3),
                SuggestedSubject = template.Subject ?? Truncate(template.Summary, 80),
                Summary = template.Summary,
                SuggestedSolution = template.Solution,
                MissingInfoJson = JsonSerializer.Serialize(template.MissingInfo, JsonOptions),
                RedactedInput = template.Subject is null ? template.Description : $"{template.Subject}\n\n{template.Description}",
                InputTokens = random.Next(250, 700),
                OutputTokens = random.Next(150, 400),
                DurationMs = random.Next(900, 4500),
                CreatedAt = classifiedAt
            };
        }
        else
        {
            // Three failed attempts (0s, +30s, +2m), then fallback to GENERAL.
            job.Status = AiJobStatus.Failed;
            job.Attempts = 3;
            job.NextAttemptAt = created.AddSeconds(150);
            job.LastErrorCode = Pick(AiErrorCodes);
            ticket.AiStatus = AiStatus.Failed;
        }

        ticket.AiAnalysisJobs.Add(job);
    }

    private static void AddHistory(Ticket ticket, TicketStatus? previous, TicketStatus next, int? changedBy, DateTime at, string? reason = null) =>
        ticket.StatusHistory.Add(new TicketStatusHistory
        {
            PreviousStatus = previous,
            NewStatus = next,
            ChangedByUserId = changedBy,
            ChangedAt = at,
            Reason = reason
        });

    private static void AddComment(Ticket ticket, int authorId, string body, DateTime at, bool isInternal = false, bool isResolution = false) =>
        ticket.Comments.Add(new TicketComment
        {
            AuthorUserId = authorId,
            Body = body,
            IsInternal = isInternal,
            IsResolution = isResolution,
            CreatedAt = at
        });

    private TicketTemplate PickTemplate(string categoryCode)
    {
        var candidates = TicketTemplates.All.Where(t => t.CategoryCode == categoryCode).ToList();
        return candidates[random.Next(candidates.Count)];
    }

    private T Pick<T>(IReadOnlyList<T> items) => items[random.Next(items.Count)];

    private List<T> Shuffle<T>(List<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }

        return items;
    }

    private TimeSpan Hours(double min, double max) => TimeSpan.FromSeconds(Math.Round((min + (max - min) * random.NextDouble()) * 3600));

    private TimeSpan Minutes(double min, double max) => Hours(min / 60, max / 60);

    private static TimeSpan Sum(IEnumerable<TimeSpan> spans) => spans.Aggregate(TimeSpan.Zero, (a, b) => a + b);

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
