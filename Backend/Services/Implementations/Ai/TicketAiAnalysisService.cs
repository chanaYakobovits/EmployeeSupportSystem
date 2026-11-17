using System.Text.Json;
using Common.Constants;
using Common.DTOs.Ai;
using Common.DTOs.Tickets;
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
/// Order of the checks of "נתח מחדש": 404 (outside every scope, or the requester's view) ← 403 (not a handler in scope)
/// ← 422 (not open / in progress / AI unavailable) ← 429 (a job of this ticket in the last two minutes).
/// Logs ids only – never the ticket text or the analysis.
/// </remarks>
public sealed class TicketAiAnalysisService(
    ITicketAccess ticketAccess,
    ITicketRepository tickets,
    IAiAnalysisJobRepository jobs,
    IAuditLogRepository auditLogs,
    IAiAvailability availability,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ICurrentUser currentUser,
    IOptions<AiOptions> options,
    ILogger<TicketAiAnalysisService> logger) : ITicketAiAnalysisService
{
    public const string RetryNotAllowedTitle = "רק מטפל בפנייה יכול לבקש ניתוח מחדש";
    public const string InProgressTitle = "הניתוח של הפנייה כבר בתהליך";
    public const string UnavailableTitle = "שירות ה-AI אינו זמין כרגע";
    public const string UnavailableDetail = "אפשר לסווג את הפנייה ידנית, או לנסות שוב מאוחר יותר.";
    public const string NotOpenTitle = "לא ניתן לנתח מחדש פנייה שנפתרה, נסגרה או בוטלה";
    public const string TooSoonTitle = "ניתן לבקש ניתוח מחדש של פנייה פעם אחת בשתי דקות";
    public const string TooSoonDetail = "יש לנסות שוב בעוד מספר דקות.";

    public async Task<TicketAiAnalysisDto> GetAsync(int ticketId, CancellationToken cancellationToken)
    {
        var access = await StaffAccessAsync(ticketId, cancellationToken);
        var row = await jobs.GetAnalysisViewAsync(access.Ticket.TicketId, cancellationToken)
                  ?? throw new NotFoundException(TicketAccess.TicketNotFoundTitle);

        return new TicketAiAnalysisDto(Processing(row), Analysis(row));
    }

    public async Task<TicketUpdatedDto> RetryAsync(int ticketId, string ipAddress, CancellationToken cancellationToken)
    {
        var access = await StaffAccessAsync(ticketId, cancellationToken);
        if (!access.Permissions.CanRetryAnalysis)
        {
            throw new ForbiddenException(RetryNotAllowedTitle, errorType: "ai-retry-not-allowed");
        }

        var ticket = await tickets.FindForUpdateAsync(ticketId, cancellationToken)
                     ?? throw new NotFoundException(TicketAccess.TicketNotFoundTitle);

        if (ticket.Status is TicketStatus.Resolved or TicketStatus.Closed or TicketStatus.Cancelled)
        {
            throw new BusinessRuleException(NotOpenTitle, errorType: "ticket-not-open");
        }

        if (ticket.AiStatus is AiStatus.Pending or AiStatus.Processing)
        {
            throw new BusinessRuleException(InProgressTitle, errorType: "ai-analysis-in-progress");
        }

        var available = availability.Check();
        if (available != AiAvailability.Available)
        {
            logger.LogInformation("AI re-analysis of ticket {TicketId} refused: {Availability}", ticketId, available);
            throw new BusinessRuleException(UnavailableTitle, UnavailableDetail, "ai-unavailable");
        }

        var now = clock.UtcNow;
        var latest = await jobs.GetLatestJobCreatedAtAsync(ticketId, cancellationToken);
        if (latest is { } last && now < last + AiRetryLimits.Interval)
        {
            throw new TooManyRequestsException(TooSoonTitle, TooSoonDetail, "ai-retry-too-soon",
                last + AiRetryLimits.Interval - now);
        }

        // Internal state only, like the worker: UpdatedAt stays. The ticket is saved under its RowVersion (read now), so
        // two requests at once queue one job – the second gets 409.
        ticket.AiStatus = AiStatus.Pending;
        jobs.AddJob(new AiAnalysisJob
        {
            OrganizationId = ticket.OrganizationId,
            TicketId = ticket.Id,
            Status = AiJobStatus.Pending,
            NextAttemptAt = now,
            CreatedAt = now,
        });
        auditLogs.Add(AuditEntries.ForEntity(AuditActions.TicketAiRetryRequested, AuditEntityTypes.Ticket, ticket.Id,
            currentUser.OrganizationId, access.UserId, ipAddress, now));

        await new TicketUpdateSteps(tickets, unitOfWork, logger).SaveAsync(ticket, "ai-retry", access.UserId,
            cancellationToken);

        logger.LogInformation("AI re-analysis of ticket {TicketId} queued by user {UserId}", ticket.Id, access.UserId);
        return TicketUpdateSteps.Result(ticket, access);
    }

    /// <summary>Staff with a scope (spec 2.4: "צפייה בניתוח AI"). The requester's view is "not found", not 403.</summary>
    private async Task<TicketAccessResult> StaffAccessAsync(int ticketId, CancellationToken cancellationToken)
    {
        var access = await ticketAccess.GetAsync(ticketId, cancellationToken);
        if (!access.Permissions.CanSeeInternal)
        {
            throw new NotFoundException(TicketAccess.TicketNotFoundTitle);
        }

        return access;
    }

    private static AiProcessingDto Processing(TicketAiAnalysisRow row)
    {
        var job = row.LatestJob;
        // The error of the latest job: when it failed, or when it waits for a retry after a transient failure.
        var errorCode = row.AiStatus is AiStatus.Failed or AiStatus.Pending ? job?.LastErrorCode : null;
        var errorMessage = row.AiStatus == AiStatus.Skipped ? AiErrorMessages.Skipped
            : errorCode is not null || row.AiStatus == AiStatus.Failed ? AiErrorMessages.For(errorCode)
            : null;
        var nextAttemptAt = row.AiStatus == AiStatus.Pending && job is { Status: AiJobStatus.Pending }
            ? job.NextAttemptAt
            : (DateTime?)null;

        return new AiProcessingDto(row.AiStatus, errorCode, errorMessage, job?.CreatedAt, nextAttemptAt);
    }

    private AiAnalysisDetailsDto? Analysis(TicketAiAnalysisRow row)
    {
        if (row.Analysis is not { } a)
        {
            return null;
        }

        return new AiAnalysisDetailsDto(
            a.CategoryCode,
            a.CategoryName,
            a.CategoryIsActive ? a.CategoryId : null,
            a.Urgency,
            a.Confidence,
            AiConfidence.LevelOf(a.Confidence, options.Value.MinConfidence),
            a.SuggestedSubject,
            a.Summary,
            MissingInformation(a.MissingInfoJson),
            a.SuggestedSolution,
            a.Model,
            a.PromptVersion,
            a.CreatedAt,
            a.Verdict,
            a.Disposition,
            Applied: a.Disposition == AiAnalysisDisposition.Applied,
            MatchesCurrentClassification: a.CategoryId is not null && a.CategoryId == row.CategoryId
                                          && a.Urgency == row.Urgency,
            a.RedactedInput);
    }

    private static IReadOnlyList<string> MissingInformation(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json)?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList()
                   ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
