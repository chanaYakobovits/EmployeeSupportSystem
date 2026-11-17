using Common.DTOs.Ai.Evaluation;
using Common.Enums;
using Services.Implementations.Ai.Evaluation;

namespace Services.Tests.Ai.Evaluation;

/// <summary>Shape and coverage of Tests/AiEvaluation/ai-evaluation-set.json (copied to the test output).</summary>
public class AiEvaluationSetTests
{
    // Same codes as Repositories/Data/Seed/ReferenceSeedData (internal there).
    private static readonly string[] CategoryCodes =
        ["PAYROLL", "ATTENDANCE", "LEAVE", "EQUIPMENT", "ADMIN", "BENEFITS", "TRAINING", "GENERAL"];

    /// <summary>Through the same loader as the evaluate endpoint (strict: an unknown field fails).</summary>
    private static AiEvaluationSet Load() =>
        AiEvaluationSetLoader.LoadAsync(Path.Combine(AppContext.BaseDirectory, "AiEvaluation", "ai-evaluation-set.json"),
            CancellationToken.None).GetAwaiter().GetResult();

    [Fact]
    public void Set_Has30Cases_WithUniqueIdsAndRequiredFields()
    {
        var cases = Load().Cases;

        Assert.Equal(30, cases.Count);
        Assert.Equal(cases.Count, cases.Select(c => c.Id).Distinct().Count());
        Assert.All(cases, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Text), c.Id);
            Assert.False(string.IsNullOrWhiteSpace(c.Note), c.Id);
            Assert.Contains(c.ExpectedCategory, CategoryCodes);
            Assert.True(Enum.IsDefined(c.ExpectedUrgency), c.Id);
        });
    }

    [Fact]
    public void Set_CoversEveryCategory_ThreeToFourCasesEach()
    {
        var byCategory = Load().Cases.GroupBy(c => c.ExpectedCategory).ToDictionary(g => g.Key, g => g.Count());

        Assert.All(CategoryCodes, code =>
            Assert.InRange(byCategory.GetValueOrDefault(code), 3, 4));
    }

    [Fact]
    public void Set_CoversEveryUrgency_AndTheRequiredSpecialCases()
    {
        var cases = Load().Cases;

        Assert.All(Enum.GetValues<Urgency>(), u => Assert.Contains(cases, c => c.ExpectedUrgency == u));
        Assert.Equal(2, cases.Count(c => c.Injection is not null));
        Assert.All(cases.Where(c => c.Tags.Contains("injection")), c => Assert.NotNull(c.Injection));
        foreach (var tag in new[] { "borderline", "pii", "short", "typos", "no-category" })
        {
            Assert.Contains(cases, c => c.Tags.Contains(tag));
        }
    }
}
