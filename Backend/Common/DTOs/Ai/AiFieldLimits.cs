namespace Common.DTOs.Ai;

/// <summary>
/// Maximum lengths of AI text fields. Single source for the prompt and the response validator.
/// Subject and summary follow spec 5.4; the rest fit the TicketAiAnalyses columns (nvarchar(4000)).
/// </summary>
public static class AiFieldLimits
{
    public const int SubjectMaxLength = 80;
    public const int SummaryMaxLength = 300;
    public const int SolutionMaxLength = 1500;
    public const int MissingInfoMaxItems = 5;
    public const int MissingInfoItemMaxLength = 150;
}
