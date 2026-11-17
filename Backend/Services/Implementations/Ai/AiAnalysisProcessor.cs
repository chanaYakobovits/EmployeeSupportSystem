using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using Common.Constants;
using Common.DTOs.Ai;
using Common.DTOs.Categories;
using Common.DTOs.Routing;
using Common.Enums;
using Common.Exceptions;
using Common.Interfaces;
using Common.Models;
using Common.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repositories.Interfaces;
using Services.Implementations.Tickets;
using Services.Interfaces;

namespace Services.Implementations.Ai;

/// <inheritdoc />
/// <remarks>
/// Flow of one job (spec 4.3, 5.2–5.6):
/// <list type="number">
/// <item>Checks that need no AI call: ticket gone / closed, crashed too often, the time guard (FallbackAfterMinutes from
/// the opening), the circuit breaker → GENERAL (or just end the job).</item>
/// <item>AiStatus Processing.</item>
/// <item>PII redaction of subject + description with the requester's and the organization's names – the only text that
/// leaves the system (spec 1.4, 5.3). Never the attachments, their names, or anything else about the requester.</item>
/// <item>IAiClient (which also validates, spec 5.5) → Valid: the AI's category and urgency, source Ai. LowConfidence /
/// UnknownCategory: GENERAL, the analysis kept as a recommendation. Transient failure: retry after 30s / 2m / 10m, then
/// GENERAL. Rejected / Disabled: GENERAL now.</item>
/// </list>
/// Every write reads the ticket again and saves under its RowVersion. A ticket a handler classified meanwhile (Manual) or
/// that is no longer New keeps its state: only the analysis is stored, as a recommendation (spec 11.2).
/// Logs ids, outcome, error code, tokens and duration – never ticket text or AI output (spec 6.4).
/// </remarks>
public sealed class AiAnalysisProcessor(
    IAiAnalysisJobRepository jobs,
    ITicketRepository tickets,
    AiInputBuilder input,
    IRoutingService routing,
    ITicketClassifier classifier,
    IAiClient aiClient,
    IAiCircuitBreaker breaker,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    IOptions<AiOptions> options,
    ILogger<AiAnalysisProcessor> logger) : IAiAnalysisProcessor
{
    private static readonly JsonSerializerOptions MissingInfoJson = new()
    {
        // Hebrew stays readable in the database.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private AiOptions Options => options.Value;

    /// <summary>
    /// Longest time one job may hold its claim: two HTTP attempts of the client plus a margin. The jobs of a batch run in
    /// parallel, so a job never waits for the others while its lock runs out.
    /// </summary>
    internal TimeSpan LockDuration => TimeSpan.FromSeconds(2 * Options.TimeoutSeconds + 60);

    public Task<IReadOnlyList<ClaimedAiJob>> ClaimDueAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        return jobs.ClaimDueJobsAsync(Options.BatchSize, now, now + LockDuration, cancellationToken);
    }

    public async Task ProcessAsync(int jobId, CancellationToken cancellationToken)
    {
        var job = await jobs.FindJobAsync(jobId, cancellationToken);
        if (job is not { Status: AiJobStatus.Processing })
        {
            logger.LogInformation("AI job {JobId} skipped: no longer processing", jobId);
            return;
        }

        var claim = job.Attempts;
        var ticket = await tickets.FindForUpdateAsync(job.TicketId, cancellationToken);

        if (ticket is null || ticket.Status is TicketStatus.Closed or TicketStatus.Cancelled)
        {
            // Nothing to classify any more – and nothing is sent.
            await EndWithoutAnalysisAsync(job.Id, claim,
                ticket is null ? AiJobErrorCodes.TicketNotFound : AiJobErrorCodes.TicketClosed, cancellationToken);
            return;
        }

        var precheck = claim > Options.MaxAttempts + 1 ? AiJobErrorCodes.Abandoned
            : clock.UtcNow >= FallbackDeadline(job) ? AiJobErrorCodes.FallbackTimeout
            : breaker.IsOpen ? AiJobErrorCodes.CircuitOpen
            : null;
        if (precheck is not null)
        {
            await FallBackAsync(job.Id, claim, precheck, null, cancellationToken);
            return;
        }

        if (await MarkProcessingAsync(job.Id, claim, cancellationToken))
        {
            await AnalyzeAsync(job.Id, claim, cancellationToken);
        }
    }

    // ===================== Steps =====================

    private async Task EndWithoutAnalysisAsync(int jobId, int claim, string errorCode,
        CancellationToken cancellationToken)
    {
        var finished = await FinishAsync(jobId, claim, (j, t, at, _) =>
        {
            if (t is not null)
            {
                t.AiStatus = AiStatus.Failed;
            }
            Complete(j, AiJobStatus.Failed, errorCode, at);
            return Task.FromResult<RoutingDecision?>(null);
        }, cancellationToken);

        if (finished is not null)
        {
            LogOutcome(jobId, finished.Value.TicketId, claim, "ended without analysis", errorCode, null);
        }
    }

    private async Task<bool> MarkProcessingAsync(int jobId, int claim, CancellationToken cancellationToken)
    {
        var finished = await FinishAsync(jobId, claim, (_, t, _, _) =>
        {
            // Internal state only: UpdatedAt is not touched (the employee would see "updated" with nothing new).
            if (t is not null)
            {
                t.AiStatus = AiStatus.Processing;
            }
            return Task.FromResult<RoutingDecision?>(null);
        }, cancellationToken);
        return finished is not null;
    }

    private async Task AnalyzeAsync(int jobId, int claim, CancellationToken cancellationToken)
    {
        unitOfWork.DiscardChanges();
        var job = await jobs.FindJobAsync(jobId, cancellationToken);
        var ticket = job is null ? null : await tickets.FindForUpdateAsync(job.TicketId, cancellationToken);
        if (ticket is null)
        {
            await EndWithoutAnalysisAsync(jobId, claim, AiJobErrorCodes.TicketNotFound, cancellationToken);
            return;
        }

        string redactedText;
        try
        {
            redactedText = await input.RedactAsync(ticket.OrganizationId, ticket.RequesterId,
                AiInputBuilder.ComposeText(ticket.Subject, ticket.Description), cancellationToken);
        }
        catch (RegexMatchTimeoutException)
        {
            // Spec 5.3: when the redaction is not certain, nothing is sent.
            await FallBackAsync(jobId, claim, AiJobErrorCodes.RedactionFailed, null, cancellationToken);
            return;
        }

        var activeCategories = await input.ActiveCategoriesAsync(ticket.OrganizationId, cancellationToken);

        var result = await aiClient.AnalyzeAsync(redactedText, AiInputBuilder.Offered(activeCategories),
            cancellationToken);
        RecordInBreaker(result);

        if (result.IsSuccess)
        {
            await SaveAnalysisAsync(jobId, claim, result, redactedText, activeCategories, cancellationToken);
            return;
        }

        var errorCode = result.ErrorCode!.Value.ToString();
        if (result.IsTransient && claim <= Options.MaxAttempts)
        {
            await ScheduleRetryAsync(jobId, claim, errorCode, result, cancellationToken);
            return;
        }

        await FallBackAsync(jobId, claim, errorCode, result, cancellationToken);
    }

    private void RecordInBreaker(AiAnalysisResult result)
    {
        if (result.IsSuccess)
        {
            breaker.RecordSuccess();
        }
        else if (result.ErrorCode != AiErrorCode.Disabled)
        {
            // Disabled made no call – it says nothing about the AI's health.
            breaker.RecordFailure();
        }
    }

    /// <summary>A usable response: stored, and applied to the ticket if it is still waiting for it.</summary>
    private async Task SaveAnalysisAsync(int jobId, int claim, AiAnalysisResult result, string redactedText,
        IReadOnlyList<CategoryDto> activeCategories, CancellationToken cancellationToken)
    {
        var analysis = result.Analysis!;
        // Valid, with a category that is still active → the AI's classification; otherwise GENERAL (spec 5.5).
        var category = AiInputBuilder.AcceptedCategory(result, activeCategories);
        var useAi = category is not null;

        var finished = await FinishAsync(jobId, claim, async (j, t, at, ct) =>
        {
            Complete(j, AiJobStatus.Succeeded, j.LastErrorCode, at);
            if (t is null)
            {
                return null;
            }

            var canClassify = CanClassify(t);
            var disposition = !canClassify
                ? t.ClassificationSource == ClassificationSource.Manual
                    ? AiAnalysisDisposition.ManuallyClassified
                    : AiAnalysisDisposition.AlreadyRouted
                : useAi ? AiAnalysisDisposition.Applied
                : result.Verdict == AiValidationVerdict.LowConfidence ? AiAnalysisDisposition.LowConfidence
                : AiAnalysisDisposition.UnknownCategory;

            await StoreAnalysisAsync(t.Id, result, analysis, redactedText, disposition, at, ct);
            t.AiStatus = AiStatus.Completed;
            SuggestSubject(t, analysis.SuggestedSubject);
            if (!canClassify)
            {
                return null;
            }

            return useAi
                ? await classifier.ApplyAsync(t,
                    new TicketClassification(category!.Id, analysis.Urgency, ClassificationSource.Ai), at, ct)
                : await FallbackClassification.ApplyAsync(routing, classifier, t, at, logger, ct);
        }, cancellationToken);

        if (finished is not { } done)
        {
            return;
        }

        var outcome = !done.Classified ? "analysis saved as a recommendation only"
            : useAi ? "classified by AI"
            : $"fallback after {result.Verdict}";
        LogOutcome(jobId, done.TicketId, claim, outcome, null, result);
        LogRouting(done);
    }

    private async Task StoreAnalysisAsync(int ticketId, AiAnalysisResult result, AiAnalysis analysis,
        string redactedText, AiAnalysisDisposition disposition, DateTime now, CancellationToken cancellationToken)
    {
        // One analysis per ticket (TicketId unique, spec 8.3): a later analysis replaces the earlier one.
        var row = await jobs.FindAnalysisAsync(ticketId, cancellationToken);
        if (row is null)
        {
            row = new TicketAiAnalysis { TicketId = ticketId };
            jobs.AddAnalysis(row);
        }

        row.Model = Cut(result.Model, 50);
        row.PromptVersion = Cut(result.PromptVersion, 20);
        row.CategoryCode = Cut(analysis.CategoryCode, 30);
        row.Urgency = analysis.Urgency;
        row.Confidence = Math.Round(analysis.Confidence, 3);
        row.SuggestedSubject = analysis.SuggestedSubject;
        row.Summary = analysis.Summary;
        row.SuggestedSolution = analysis.SuggestedSolution;
        row.MissingInfoJson = JsonSerializer.Serialize(analysis.MissingInformation, MissingInfoJson);
        row.Verdict = result.Verdict ?? AiValidationVerdict.Valid;
        row.Disposition = disposition;
        row.RedactedInput = redactedText;
        row.InputTokens = result.InputTokens;
        row.OutputTokens = result.OutputTokens;
        row.DurationMs = result.DurationMs;
        row.CreatedAt = now;
    }

    private async Task ScheduleRetryAsync(int jobId, int claim, string errorCode, AiAnalysisResult result,
        CancellationToken cancellationToken)
    {
        DateTime nextAttemptAt = default;
        var finished = await FinishAsync(jobId, claim, (j, t, at, _) =>
        {
            var next = at.AddSeconds(RetryDelaySeconds(claim));
            // Never later than the time guard: then the ticket goes to GENERAL on time (spec 5.6).
            var deadline = FallbackDeadline(j);
            next = next < deadline ? next : deadline;
            if (t is not null)
            {
                t.AiStatus = AiStatus.Pending;
            }

            j.Status = AiJobStatus.Pending;
            j.NextAttemptAt = next;
            j.LockedUntil = null;
            j.LastErrorCode = errorCode;
            nextAttemptAt = next;
            return Task.FromResult<RoutingDecision?>(null);
        }, cancellationToken);

        if (finished is not null)
        {
            LogOutcome(jobId, finished.Value.TicketId, claim, $"retry at {nextAttemptAt:o}", errorCode, result);
        }
    }

    /// <summary>GENERAL (spec 5.6) – unless the ticket is no longer waiting for the AI. The job ends as Failed.</summary>
    private async Task FallBackAsync(int jobId, int claim, string errorCode, AiAnalysisResult? result,
        CancellationToken cancellationToken)
    {
        var finished = await FinishAsync(jobId, claim, async (j, t, at, ct) =>
        {
            Complete(j, AiJobStatus.Failed, errorCode, at);
            if (t is null)
            {
                return null;
            }

            t.AiStatus = AiStatus.Failed;
            return CanClassify(t)
                ? await FallbackClassification.ApplyAsync(routing, classifier, t, at, logger, ct)
                : null;
        }, cancellationToken);

        if (finished is not { } done)
        {
            return;
        }

        LogOutcome(jobId, done.TicketId, claim, done.Classified ? "fallback" : "failed, ticket left as is", errorCode,
            result);
        LogRouting(done);
    }

    /// <summary>
    /// Spec 3.1: a ticket opened without a subject gets the AI's suggestion – also when the analysis is only a
    /// recommendation (the subject says what the ticket is about, not how it is classified). Never over a subject the
    /// employee wrote, and never one with a redaction tag ("[NAME]" would reach the employee's screen). Like AiStatus,
    /// not an update the employee should see as activity: UpdatedAt stays.
    /// </summary>
    private static void SuggestSubject(Ticket ticket, string suggestedSubject)
    {
        var subject = suggestedSubject.Trim();
        if (ticket.Subject is not null || subject.Length == 0 || RedactionTag.IsMatch(subject))
        {
            return;
        }

        ticket.Subject = Cut(subject, TicketFieldLimits.SubjectMaxLength);
    }

    private static readonly Regex RedactionTag =
        new(@"\[(NAME|EMAIL|PHONE|ID|NUMBER|EMPLOYEE_CODE)\]", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    // ===================== Saving =====================

    private delegate Task<RoutingDecision?> JobUpdate(AiAnalysisJob job, Ticket? ticket, DateTime now,
        CancellationToken cancellationToken);

    /// <param name="Classified">The update classified the ticket (Ai or Fallback) – false for a recommendation only.</param>
    private readonly record struct Finished(int TicketId, bool Classified, RoutingDecision? Decision,
        ClassificationSource? Source);

    /// <summary>
    /// Reads the job and its ticket fresh, applies <paramref name="update"/> and saves both in one transaction – the
    /// ticket under its RowVersion (spec 11.2), the job under its claim (Attempts). A conflict (a handler or the employee
    /// changed the ticket meanwhile) → once more from the new state, where the rules decide again (e.g. it is no longer
    /// New → recommendation only). Null when the job is no longer ours (claimed again after its lock expired).
    /// </summary>
    private async Task<Finished?> FinishAsync(int jobId, int claim, JobUpdate update,
        CancellationToken cancellationToken)
    {
        for (var round = 1; ; round++)
        {
            unitOfWork.DiscardChanges();
            var job = await jobs.FindJobAsync(jobId, cancellationToken);
            if (job is null || job.Attempts != claim || job.Status != AiJobStatus.Processing)
            {
                logger.LogWarning("AI job {JobId}: claimed again by another worker; this result is not saved", jobId);
                return null;
            }

            var ticket = await tickets.FindForUpdateAsync(job.TicketId, cancellationToken);
            var wasSource = ticket?.ClassificationSource;
            var wasStatus = ticket?.Status;
            var decision = await update(job, ticket, clock.UtcNow, cancellationToken);

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConflictException exception) when (exception.ErrorType == "concurrency" && round < 2)
            {
                logger.LogInformation("AI job {JobId}: ticket {TicketId} changed meanwhile; applying to its new state",
                    jobId, job.TicketId);
                continue;
            }

            var classified = ticket is not null
                             && (ticket.Status != wasStatus || ticket.ClassificationSource != wasSource);
            return new Finished(job.TicketId, classified, decision, ticket?.ClassificationSource);
        }
    }

    // ===================== Rules =====================

    /// <summary>
    /// Spec 11.2: the AI classifies only a ticket still waiting for it – New, and not classified by a handler. Anything
    /// else keeps the people's decision; the analysis is only a recommendation.
    /// </summary>
    private static bool CanClassify(Ticket ticket) =>
        ticket.Status == TicketStatus.New && ticket.ClassificationSource != ClassificationSource.Manual;

    /// <summary>
    /// The time guard (spec 5.6), from the job's creation: the ticket's opening for its first job, the request for a
    /// "נתח מחדש" job (task 4.4) – otherwise a re-analysis of a ticket older than the guard would never call the AI.
    /// </summary>
    private DateTime FallbackDeadline(AiAnalysisJob job) => job.CreatedAt.AddMinutes(Options.FallbackAfterMinutes);

    /// <summary>Delay before the retry that follows failed attempt n (1-based). The last value repeats.</summary>
    private int RetryDelaySeconds(int failedAttempts)
    {
        var delays = Options.RetryDelaysSeconds;
        return delays[Math.Min(failedAttempts, delays.Length) - 1];
    }

    private static void Complete(AiAnalysisJob job, AiJobStatus status, string? errorCode, DateTime now)
    {
        job.Status = status;
        job.LastErrorCode = errorCode;
        job.CompletedAt = now;
        job.LockedUntil = null;
    }

    private static string Cut(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];

    private void LogRouting(Finished done)
    {
        if (done.Decision is not null && done.Source is { } source)
        {
            RoutingLog.Routed(logger, done.TicketId, done.Decision, source);
        }
    }

    private void LogOutcome(int jobId, int ticketId, int attempt, string outcome, string? errorCode,
        AiAnalysisResult? result) =>
        logger.LogInformation(
            "AI job {JobId} for ticket {TicketId}: {Outcome}, ErrorCode={ErrorCode} Attempt={Attempt} " +
            "InputTokens={InputTokens} OutputTokens={OutputTokens} DurationMs={DurationMs}",
            jobId, ticketId, outcome, errorCode, attempt, result?.InputTokens ?? 0, result?.OutputTokens ?? 0,
            result?.DurationMs ?? 0);
}
