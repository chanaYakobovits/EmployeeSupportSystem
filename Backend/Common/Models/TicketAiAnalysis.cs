using Common.Enums;

namespace Common.Models;

public class TicketAiAnalysis
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public string Model { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public Urgency Urgency { get; set; }
    public decimal Confidence { get; set; }
    public string SuggestedSubject { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string SuggestedSolution { get; set; } = string.Empty;
    public string MissingInfoJson { get; set; } = string.Empty;

    /// <summary>The ticket text exactly as sent to the AI, after PII redaction.</summary>
    public string RedactedInput { get; set; } = string.Empty;

    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int DurationMs { get; set; }
    public DateTime CreatedAt { get; set; }

    public Ticket Ticket { get; set; } = null!;
}
