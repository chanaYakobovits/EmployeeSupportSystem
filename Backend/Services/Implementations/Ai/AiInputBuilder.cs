using Common.DTOs.Ai;
using Common.DTOs.Categories;
using Common.Enums;
using Repositories.Interfaces;
using Services.Interfaces;

namespace Services.Implementations.Ai;

/// <summary>
/// What goes to the AI and how its answer is decided – in one place, for the background worker
/// (<see cref="AiAnalysisProcessor"/>) and the development evaluation endpoints, so both measure the same path.
/// </summary>
public sealed class AiInputBuilder(
    IUserRepository users,
    ICategoryRepository categories,
    IReferenceDataCache cache,
    IPiiRedactor redactor)
{
    /// <summary>The employee's own text only: subject (if any), a blank line, and the description.</summary>
    public static string ComposeText(string? subject, string description) =>
        subject is null ? description : $"{subject}\n\n{description}";

    /// <summary>
    /// PII redaction (spec 5.3) with the requester's names and the names of everyone in the organization.
    /// The result is the only text that leaves the system.
    /// </summary>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">
    /// Redaction did not complete in time – nothing may be sent.
    /// </exception>
    public async Task<string> RedactAsync(int organizationId, int requesterId, string text,
        CancellationToken cancellationToken)
    {
        var requesterNames = await users.GetNamePartsAsync(requesterId, cancellationToken);
        var organizationNames = await ReferenceDataLookups.UserNamesAsync(cache, users, organizationId,
            cancellationToken);
        return redactor.Redact(text, requesterNames, organizationNames).RedactedText;
    }

    /// <summary>The organization's active categories (from the cache) – the closed list offered to the AI.</summary>
    public async Task<IReadOnlyList<CategoryDto>> ActiveCategoriesAsync(int organizationId,
        CancellationToken cancellationToken) =>
        (await ReferenceDataLookups.CategoriesAsync(cache, categories, organizationId, cancellationToken))
        .Where(c => c.IsActive).ToList();

    public static IReadOnlyList<AiCategory> Offered(IReadOnlyList<CategoryDto> activeCategories) =>
        activeCategories.Select(c => new AiCategory(c.Code, c.Name, c.Description)).ToList();

    /// <summary>
    /// The decision on a successful analysis (spec 5.5): Valid with a category that is still active → that category;
    /// null → the fallback (GENERAL), the analysis kept as a recommendation.
    /// </summary>
    public static CategoryDto? AcceptedCategory(AiAnalysisResult result, IReadOnlyList<CategoryDto> activeCategories) =>
        result is { IsSuccess: true, Verdict: AiValidationVerdict.Valid, Analysis: { } analysis }
            ? activeCategories.FirstOrDefault(c => c.Code == analysis.CategoryCode)
            : null;
}
