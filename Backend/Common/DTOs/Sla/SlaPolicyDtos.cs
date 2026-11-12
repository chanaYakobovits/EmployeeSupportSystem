namespace Common.DTOs.Sla;

/// <summary>GET /api/sla-policies: resolution target in calendar hours per urgency (spec 3.4).</summary>
public sealed record SlaPoliciesDto(int Critical, int High, int Medium, int Low);

/// <summary>
/// PUT /api/sla-policies: all four levels together. Whole positive hours, Critical ≤ High ≤ Medium ≤ Low.
/// Applies to new tickets and to tickets whose urgency changes; existing DueAt values are not recalculated.
/// </summary>
public sealed record UpdateSlaPoliciesRequest(int? Critical, int? High, int? Medium, int? Low);
