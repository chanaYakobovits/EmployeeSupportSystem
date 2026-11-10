namespace Common.DTOs.Ai;

/// <summary>An active category offered to the AI. Organization-defined reference data, not personal data.</summary>
public sealed record AiCategory(string Code, string Name, string Description);
