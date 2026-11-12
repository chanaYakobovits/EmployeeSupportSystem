using Common.DTOs.Paging;
using FluentValidation;

namespace Services.Validators;

/// <summary>Spec 9.4: page ≥ 1, pageSize 1..100 (otherwise 400), sortBy from the list's closed set, sortDir asc|desc.</summary>
public abstract class PageQueryValidator<T> : AbstractValidator<T>
    where T : PageQuery
{
    public const string PageMessage = "מספר העמוד חייב להיות 1 ומעלה";
    public static readonly string PageSizeMessage = $"גודל העמוד חייב להיות בין 1 ל-{PageQuery.MaxPageSize}";
    public const string SortDirMessage = "כיוון המיון חייב להיות asc או desc";

    protected PageQueryValidator(IReadOnlyList<string> sortFields)
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1).WithMessage(PageMessage);

        RuleFor(q => q.PageSize).InclusiveBetween(1, PageQuery.MaxPageSize).WithMessage(PageSizeMessage);

        // Exact match: the value is used as is in the repository's switch.
        RuleFor(q => q.SortBy)
            .Must(sortBy => sortBy is null || sortFields.Contains(sortBy))
            .WithMessage($"ניתן למיין רק לפי: {string.Join(", ", sortFields)}");

        RuleFor(q => q.SortDir)
            .Must(dir => dir is null
                         || string.Equals(dir, PageQuery.Ascending, StringComparison.OrdinalIgnoreCase)
                         || string.Equals(dir, PageQuery.Descending, StringComparison.OrdinalIgnoreCase))
            .WithMessage(SortDirMessage);
    }
}
