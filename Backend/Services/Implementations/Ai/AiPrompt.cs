using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Common.DTOs.Ai;

namespace Services.Implementations.Ai;

/// <summary>
/// The versioned system prompt (spec 5.4). The template is an embedded resource named after
/// <see cref="CurrentVersion"/>, so every stored analysis can be traced to the exact prompt text.
/// </summary>
public static class AiPrompt
{
    /// <summary>Stored in TicketAiAnalyses.PromptVersion (max 20 chars). Bump together with a new Prompts/*.md file.</summary>
    public const string CurrentVersion = "ticket-analysis.v1";

    private static readonly Lazy<string> Template = new(LoadTemplate);

    // Keep Hebrew readable in the prompt (no \uXXXX escapes); the output is a prompt, not HTML.
    private static readonly JsonSerializerOptions CategoryJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Builds the system message. Contains categories and rules only – never ticket text.</summary>
    public static string BuildSystemMessage(IReadOnlyList<AiCategory> categories)
    {
        var categoryLines = new StringBuilder();
        foreach (var category in categories)
        {
            categoryLines.AppendLine(JsonSerializer.Serialize(
                new { code = category.Code, name = category.Name, description = category.Description },
                CategoryJsonOptions));
        }

        return Template.Value
            .Replace("{{CATEGORIES}}", categoryLines.ToString().TrimEnd())
            .Replace("{{SUBJECT_MAX}}", AiFieldLimits.SubjectMaxLength.ToString())
            .Replace("{{SUMMARY_MAX}}", AiFieldLimits.SummaryMaxLength.ToString())
            .Replace("{{SOLUTION_MAX}}", AiFieldLimits.SolutionMaxLength.ToString())
            .Replace("{{MISSING_MAX_ITEMS}}", AiFieldLimits.MissingInfoMaxItems.ToString())
            .Replace("{{MISSING_ITEM_MAX}}", AiFieldLimits.MissingInfoItemMaxLength.ToString());
    }

    private static string LoadTemplate()
    {
        var assembly = typeof(AiPrompt).Assembly;
        var suffix = $".Prompts.{CurrentVersion}.md";
        var resourceName = assembly.GetManifestResourceNames().SingleOrDefault(n => n.EndsWith(suffix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Embedded prompt resource '*{suffix}' was not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
