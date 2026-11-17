using Common.DTOs.Ai.Evaluation;
using Common.Enums;
using Common.Exceptions;
using Common.Options;
using Microsoft.Extensions.Options;
using Services.Implementations.Ai.Evaluation;
using Services.Tests.Ai.Fakes;
using Services.Tests.Auth.Fakes;
using Services.Tests.Fakes;
using Services.Validators;

namespace Services.Tests.Ai.Evaluation;

/// <summary>The dev evaluation goes through the worker's path and changes nothing. Noa is the signed-in user.</summary>
public sealed class AiEvaluationServiceTests : AiTestBase, IDisposable
{
    private readonly CapturingLogger<AiEvaluationService> _logger = new();
    private readonly string _datasetPath = Path.Combine(Path.GetTempPath(), $"ai-eval-{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_datasetPath);

    private AiEvaluationService Service()
    {
        var db = CreateContext(NoaId);
        return new AiEvaluationService(InputBuilderFor(db), AiClient,
            new FakeCurrentUser { SignedInUserId = NoaId, SignedInOrganizationId = OrgA },
            new DevAiAnalyzeRequestValidator(), Clock, Options.Create(Ai),
            Options.Create(new AiEvaluationOptions { DatasetPath = _datasetPath }), _logger);
    }

    [Fact]
    public async Task Analyze_SendsTheRedactedText_AndReturnsTheDecision()
    {
        AiClient.Returns(FakeAiClient.Analysis("PAYROLL", Urgency.High));

        var result = await Service().AnalyzeAsync(
            new DevAiAnalyzeRequest(" תלוש ", "נועה כאן. אלון ביטון אמר לי לפנות, הטלפון 052-7654321 "), CancellationToken.None);

        var sent = Assert.Single(AiClient.SentTexts);
        Assert.Equal(sent, result.RedactedText);
        Assert.StartsWith("תלוש\n\n", sent);
        Assert.DoesNotContain("נועה", sent);
        Assert.DoesNotContain("אלון", sent);
        Assert.DoesNotContain("052", sent);
        Assert.Contains("[NAME]", sent);
        Assert.Contains("[PHONE]", sent);
        Assert.Equal(new AiDecisionDto("PAYROLL", ClassificationSource.Ai), result.Decision);
        Assert.Equal(AiValidationVerdict.Valid, result.Verdict);
        Assert.Equal(321, result.InputTokens);
        Assert.Equal(["GENERAL", "PAYROLL"], AiClient.SentCategories.Single().Select(c => c.Code).Order());
        AssertNothingSaved();
        Assert.DoesNotContain("ביטון", _logger.AllText);
    }

    [Fact]
    public async Task Analyze_LowConfidence_KeepsTheAnalysis_DecisionIsTheFallback()
    {
        AiClient.Returns(FakeAiClient.Analysis("PAYROLL", confidence: 0.4m, verdict: AiValidationVerdict.LowConfidence));

        var result = await Service().AnalyzeAsync(new DevAiAnalyzeRequest(null, "משהו"), CancellationToken.None);

        Assert.NotNull(result.Analysis);
        Assert.Equal(AiValidationVerdict.LowConfidence, result.Verdict);
        Assert.Equal(new AiDecisionDto("GENERAL", ClassificationSource.Fallback), result.Decision);
    }

    [Fact]
    public async Task Analyze_FailedCall_ReturnsTheErrorCode()
    {
        AiClient.Returns(FakeAiClient.Failure(AiErrorCode.Timeout));

        var result = await Service().AnalyzeAsync(new DevAiAnalyzeRequest(null, "משהו"), CancellationToken.None);

        Assert.Null(result.Analysis);
        Assert.Equal("Timeout", result.ErrorCode);
        Assert.Equal(ClassificationSource.Fallback, result.Decision.Source);
        Assert.Equal(FakeAiClient.Model, result.Model);
    }

    [Fact]
    public async Task Analyze_EmptyText_Is400_WithoutACall()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            Service().AnalyzeAsync(new DevAiAnalyzeRequest(null, "  "), CancellationToken.None));

        Assert.Contains("text", exception.Errors!.Keys);
        Assert.Equal(0, AiClient.Calls);
    }

    [Fact]
    public async Task Evaluate_RunsEveryCaseInOrder_AndScores()
    {
        await File.WriteAllTextAsync(_datasetPath, """
            { "cases": [
              { "id": "A", "subject": "נושא", "text": "ראשון", "expectedCategory": "PAYROLL", "expectedUrgency": "High",
                "note": "n", "tags": [], "injection": null },
              { "id": "B", "subject": null, "text": "שני", "expectedCategory": "GENERAL", "expectedUrgency": "Low",
                "note": "n", "tags": ["no-category"], "injection": null },
              { "id": "C", "subject": null, "text": "שלישי", "expectedCategory": "PAYROLL", "expectedUrgency": "Low",
                "note": "n", "tags": ["injection"], "injection": { "targetCategory": "GENERAL", "targetUrgency": null, "marker": null } }
            ] }
            """);
        AiClient.Returns(
            FakeAiClient.Analysis("PAYROLL", Urgency.High),
            FakeAiClient.Analysis("PAYROLL", Urgency.Low, 0.3m, AiValidationVerdict.LowConfidence),
            FakeAiClient.Failure(AiErrorCode.RateLimited));

        var report = await Service().EvaluateAsync(CancellationToken.None);

        Assert.Equal(["נושא\n\nראשון", "שני", "שלישי"], AiClient.SentTexts);
        Assert.Equal(3, report.CaseCount);
        Assert.Equal(2, report.CategoryCorrect);   // A by the AI, B by the fallback
        Assert.Equal(1, report.FailedCalls);
        Assert.Equal("C", Assert.Single(report.Mistakes).Id);
        Assert.False(Assert.Single(report.Injections).Succeeded);
        Assert.Equal(FakeAiClient.PromptVersion, report.PromptVersion);
        AssertNothingSaved();
        Assert.Contains("Cases=3", _logger.AllText);
        Assert.DoesNotContain("ראשון", _logger.AllText);
    }

    [Fact]
    public async Task Evaluate_MissingSetFile_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Service().EvaluateAsync(CancellationToken.None));
        Assert.Equal(0, AiClient.Calls);
    }

    private void AssertNothingSaved()
    {
        using var db = SystemContext();
        Assert.Empty(db.TicketAiAnalyses);
        Assert.Empty(db.AiAnalysisJobs);
        Assert.Empty(db.Tickets);
    }
}
