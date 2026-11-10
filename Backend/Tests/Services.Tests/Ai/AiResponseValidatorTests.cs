using System.Text.Json;
using Common.DTOs.Ai;
using Common.Enums;
using Services.Implementations.Ai;

namespace Services.Tests.Ai;

public class AiResponseValidatorTests
{
    private static readonly string[] Codes = ["PAYROLL", "IT", "GENERAL"];
    private const decimal MinConfidence = 0.6m;

    private readonly AiResponseValidator _validator = new();

    private static string Json(
        string categoryCode = "PAYROLL", object? urgency = null, object? confidence = null,
        string subject = "נושא", string summary = "סיכום", string solution = "פתרון", string[]? missing = null) =>
        JsonSerializer.Serialize(new
        {
            categoryCode,
            urgency = urgency ?? "Medium",
            confidence = confidence ?? 0.9m,
            suggestedSubject = subject,
            summary,
            suggestedSolution = solution,
            missingInformation = missing ?? []
        });

    private AiValidationOutcome Validate(string json) => _validator.Validate(json, Codes, MinConfidence);

    // ---------- Verdicts ----------

    [Fact]
    public void WellFormed_IsValid()
    {
        var outcome = Validate(Json(urgency: "Critical", missing: ["תלוש שכר"]));

        Assert.Equal(AiValidationVerdict.Valid, outcome.Verdict);
        var analysis = Assert.IsType<AiAnalysis>(outcome.Analysis);
        Assert.Equal("PAYROLL", analysis.CategoryCode);
        Assert.Equal(Urgency.Critical, analysis.Urgency);
        Assert.Equal(0.9m, analysis.Confidence);
        Assert.Equal(["תלוש שכר"], analysis.MissingInformation);
    }

    public static TheoryData<string> InvalidJson => new()
    {
        "",
        "not json",
        "[]",
        """{"categoryCode":"PAYROLL"}""",                                   // missing fields
        Json(urgency: "Urgent"),                                             // unknown urgency
        Json(urgency: "high"),                                               // case must match the schema enum
        Json(urgency: 3),                                                    // wrong type
        Json(confidence: 1.5m),                                              // out of range
        Json(confidence: -0.1m),
        Json(confidence: "0.9"),                                             // wrong type
        Json(subject: "   "),                                                // empty after trim
        Json(summary: ""),
        Json(solution: ""),
        Json(categoryCode: ""),
        """{"categoryCode":"PAYROLL","urgency":"Low","confidence":0.9,"suggestedSubject":"a","summary":"b","suggestedSolution":"c","missingInformation":[1]}""",
        """{"categoryCode":"PAYROLL","urgency":"Low","confidence":0.9,"suggestedSubject":"a","summary":"b","suggestedSolution":"c","missingInformation":"x"}"""
    };

    [Theory]
    [MemberData(nameof(InvalidJson))]
    public void Malformed_IsInvalid(string json)
    {
        var outcome = Validate(json);

        Assert.Equal(AiValidationVerdict.Invalid, outcome.Verdict);
        Assert.Null(outcome.Analysis);
    }

    [Theory]
    [InlineData("LEGAL")]
    [InlineData("payroll")] // codes compare ordinally
    public void CategoryNotInList_IsUnknownCategory_WithAnalysis(string code)
    {
        var outcome = Validate(Json(categoryCode: code));

        Assert.Equal(AiValidationVerdict.UnknownCategory, outcome.Verdict);
        Assert.Equal(code, outcome.Analysis!.CategoryCode);
    }

    [Fact]
    public void UnknownCategory_TakesPrecedenceOverLowConfidence()
    {
        var outcome = Validate(Json(categoryCode: "LEGAL", confidence: 0.1m));

        Assert.Equal(AiValidationVerdict.UnknownCategory, outcome.Verdict);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.59)]
    public void BelowMinConfidence_IsLowConfidence_WithAnalysis(double confidence)
    {
        var outcome = Validate(Json(confidence: (decimal)confidence));

        Assert.Equal(AiValidationVerdict.LowConfidence, outcome.Verdict);
        Assert.NotNull(outcome.Analysis);
    }

    [Theory]
    [InlineData(0.6)]
    [InlineData(1.0)]
    public void AtOrAboveMinConfidence_IsValid(double confidence)
    {
        Assert.Equal(AiValidationVerdict.Valid, Validate(Json(confidence: (decimal)confidence)).Verdict);
    }

    // ---------- Trimming ----------

    [Fact]
    public void LongFields_AreTrimmedToLimits()
    {
        var outcome = Validate(Json(
            subject: new string('א', 200),
            summary: new string('ב', 500),
            solution: new string('ג', 3000)));

        Assert.Equal(AiValidationVerdict.Valid, outcome.Verdict);
        var analysis = outcome.Analysis!;
        Assert.Equal(AiFieldLimits.SubjectMaxLength, analysis.SuggestedSubject.Length);
        Assert.Equal(AiFieldLimits.SummaryMaxLength, analysis.Summary.Length);
        Assert.Equal(AiFieldLimits.SolutionMaxLength, analysis.SuggestedSolution.Length);
    }

    [Fact]
    public void Fields_AreWhitespaceTrimmed()
    {
        var outcome = Validate(Json(categoryCode: " IT ", subject: "  נושא  ", summary: "\nסיכום\n"));

        Assert.Equal(AiValidationVerdict.Valid, outcome.Verdict);
        Assert.Equal("IT", outcome.Analysis!.CategoryCode);
        Assert.Equal("נושא", outcome.Analysis.SuggestedSubject);
        Assert.Equal("סיכום", outcome.Analysis.Summary);
    }

    [Fact]
    public void MissingInformation_IsCappedAndBlankItemsRemoved()
    {
        string[] missing = ["", "   ", "1", "2", new string('ד', 400), "4", "5", "6", "7"];

        var analysis = Validate(Json(missing: missing)).Analysis!;

        Assert.Equal(AiFieldLimits.MissingInfoMaxItems, analysis.MissingInformation.Count);
        Assert.Equal(["1", "2"], analysis.MissingInformation.Take(2));
        Assert.Equal(AiFieldLimits.MissingInfoItemMaxLength, analysis.MissingInformation[2].Length);
        Assert.Equal(["4", "5"], analysis.MissingInformation.Skip(3));
    }

    [Fact]
    public void Truncate_DoesNotSplitSurrogatePair()
    {
        // 79 letters + an emoji (2 UTF-16 units) straddles the 80 limit.
        var text = new string('א', 79) + "😀" + "סוף";

        var trimmed = AiResponseValidator.Truncate(text, AiFieldLimits.SubjectMaxLength);

        Assert.Equal(79, trimmed.Length);
        Assert.False(char.IsHighSurrogate(trimmed[^1]));
    }
}
