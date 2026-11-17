using System.Diagnostics;
using System.Text.RegularExpressions;
using Common.Constants;
using Common.DTOs.Ai.Evaluation;
using Common.DTOs.Categories;
using Common.Enums;
using Common.Exceptions;
using Common.Interfaces;
using Common.Options;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Services.Interfaces;
using Services.Validators;

namespace Services.Implementations.Ai.Evaluation;

/// <inheritdoc />
/// <remarks>
/// The path is the worker's (<see cref="AiAnalysisProcessor"/>): <see cref="AiInputBuilder"/> composes and redacts the
/// text and supplies the active categories, <see cref="IAiClient"/> calls OpenAI and validates, and
/// <see cref="AiInputBuilder.AcceptedCategory"/> decides. What the worker does around it – the job, the ticket, routing,
/// the circuit breaker, retries – is left out on purpose: nothing here may change state.
/// Logs counts, error codes, tokens and duration only – never a text or AI output (also in development).
/// </remarks>
public sealed class AiEvaluationService(
    AiInputBuilder input,
    IAiClient aiClient,
    ICurrentUser currentUser,
    IValidator<DevAiAnalyzeRequest> validator,
    IDateTimeProvider clock,
    IOptions<AiOptions> aiOptions,
    IOptions<AiEvaluationOptions> options,
    ILogger<AiEvaluationService> logger) : IAiEvaluationService
{
    public async Task<DevAiAnalyzeResultDto> AnalyzeAsync(DevAiAnalyzeRequest request,
        CancellationToken cancellationToken)
    {
        await validator.EnsureValidAsync(request, cancellationToken);

        var subject = string.IsNullOrWhiteSpace(request.Subject) ? null : request.Subject.Trim();
        var categories = await input.ActiveCategoriesAsync(currentUser.OrganizationId, cancellationToken);
        var run = await RunAsync(subject, request.Text.Trim(), categories, cancellationToken);

        logger.LogInformation(
            "Dev AI analyze: Verdict={Verdict} ErrorCode={ErrorCode} Decision={DecisionSource} " +
            "InputTokens={InputTokens} OutputTokens={OutputTokens} DurationMs={DurationMs}",
            run.Result?.Verdict, run.ErrorCode, run.Decision.Source, run.Result?.InputTokens ?? 0,
            run.Result?.OutputTokens ?? 0, run.Result?.DurationMs ?? 0);

        return new DevAiAnalyzeResultDto(run.RedactedText, run.Analysis, run.Analysis is null ? null : run.Result!.Verdict,
            run.ErrorCode, run.Decision, run.Result?.Model ?? aiOptions.Value.Model,
            run.Result?.PromptVersion ?? AiPrompt.CurrentVersion, run.Result?.InputTokens ?? 0,
            run.Result?.OutputTokens ?? 0, run.Result?.DurationMs ?? 0);
    }

    public async Task<AiEvaluationReportDto> EvaluateAsync(CancellationToken cancellationToken)
    {
        var path = options.Value.DatasetPath;
        if (!File.Exists(path))
        {
            throw new NotFoundException("קובץ סט הבדיקה לא נמצא", $"AiEvaluation:DatasetPath = {path}");
        }

        var set = await AiEvaluationSetLoader.LoadAsync(path, cancellationToken);
        var categories = await input.ActiveCategoriesAsync(currentUser.OrganizationId, cancellationToken);
        var runAt = clock.UtcNow;
        var startedAt = Stopwatch.GetTimestamp();

        // One after the other: the measurement must not depend on rate limits or on the order of parallel calls.
        var runs = new List<(AiEvaluationCase, AiCaseRun)>(set.Cases.Count);
        foreach (var evaluationCase in set.Cases)
        {
            runs.Add((evaluationCase,
                await RunAsync(evaluationCase.Subject, evaluationCase.Text, categories, cancellationToken)));
        }

        var first = runs.Select(r => r.Item2.Result).FirstOrDefault(r => r is not null);
        var report = AiEvaluationScorer.Score(runs, runAt,
            (int)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, first?.Model ?? aiOptions.Value.Model,
            first?.PromptVersion ?? AiPrompt.CurrentVersion, options.Value);

        var errorCodes = runs.Where(r => r.Item2.ErrorCode is not null)
            .GroupBy(r => r.Item2.ErrorCode!)
            .Select(g => $"{g.Key}:{g.Count()}");
        logger.LogInformation(
            "Dev AI evaluation: Cases={Cases} CategoryCorrect={CategoryCorrect} RawCategoryCorrect={RawCategoryCorrect} " +
            "UrgencyExact={UrgencyExact} UrgencyWithinOne={UrgencyWithinOne} FailedCalls={FailedCalls} " +
            "InjectionsSucceeded={InjectionsSucceeded} ErrorCodes={ErrorCodes} InputTokens={InputTokens} " +
            "OutputTokens={OutputTokens} DurationMs={DurationMs} PromptVersion={PromptVersion}",
            report.CaseCount, report.CategoryCorrect, report.RawCategoryCorrect, report.UrgencyExactCount,
            report.UrgencyWithinOneCount, report.FailedCalls, report.Injections.Count(i => i.Succeeded),
            string.Join(",", errorCodes), report.TotalInputTokens, report.TotalOutputTokens, report.DurationMs,
            report.PromptVersion);

        return report;
    }

    private async Task<AiCaseRun> RunAsync(string? subject, string text, IReadOnlyList<CategoryDto> categories,
        CancellationToken cancellationToken)
    {
        var fallback = new AiDecisionDto(categories.FirstOrDefault(c => c.IsFallback)?.Code, ClassificationSource.Fallback);

        string redactedText;
        try
        {
            redactedText = await input.RedactAsync(currentUser.OrganizationId, currentUser.UserId,
                AiInputBuilder.ComposeText(subject, text), cancellationToken);
        }
        catch (RegexMatchTimeoutException)
        {
            // As in the worker: redaction that did not finish means nothing is sent.
            return new AiCaseRun(null, null, AiJobErrorCodes.RedactionFailed, fallback);
        }

        var result = await aiClient.AnalyzeAsync(redactedText, AiInputBuilder.Offered(categories), cancellationToken);
        if (!result.IsSuccess)
        {
            return new AiCaseRun(redactedText, result, result.ErrorCode!.Value.ToString(), fallback);
        }

        var accepted = AiInputBuilder.AcceptedCategory(result, categories);
        return new AiCaseRun(redactedText, result, null,
            accepted is null ? fallback : new AiDecisionDto(accepted.Code, ClassificationSource.Ai));
    }
}
