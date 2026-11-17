using API.Auth;
using API.Filters;
using Common.DTOs.Ai.Evaluation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers.Dev;

/// <summary>
/// Development only (not in the spec): measuring the AI classification through the background worker's path.
/// Every call goes to OpenAI and costs money – evaluate runs the whole set (Tests/AiEvaluation). Nothing is saved.
/// </summary>
[ApiController]
[Route("api/dev/ai")]
[DevelopmentOnly]
[Authorize(Policy = Policies.Admin)]
public class DevAiController(IAiEvaluationService evaluation) : ControllerBase
{
    /// <summary>One text: the redacted text that was sent, the analysis, the verdict and the decision.</summary>
    [HttpPost("analyze")]
    public Task<DevAiAnalyzeResultDto> Analyze(DevAiAnalyzeRequest request, CancellationToken cancellationToken) =>
        evaluation.AnalyzeAsync(request, cancellationToken);

    /// <summary>The whole evaluation set, one case after the other, and the report.</summary>
    [HttpPost("evaluate")]
    public Task<AiEvaluationReportDto> Evaluate(CancellationToken cancellationToken) =>
        evaluation.EvaluateAsync(cancellationToken);
}
