using Common.DTOs.Sla;
using FluentValidation;

namespace Services.Validators;

/// <summary>
/// SLA targets (spec 3.4): whole positive hours, at most a year, Critical ≤ High ≤ Medium ≤ Low.
/// An order violation is reported on the less urgent level – the one whose value is out of place.
/// </summary>
public sealed class UpdateSlaPoliciesRequestValidator : AbstractValidator<UpdateSlaPoliciesRequest>
{
    public const int MaxHours = 8760;

    public UpdateSlaPoliciesRequestValidator()
    {
        Hours(r => r.Critical, "קריטית");
        Hours(r => r.High, "גבוהה");
        Hours(r => r.Medium, "בינונית");
        Hours(r => r.Low, "נמוכה");

        Order(r => r.High, r => r.Critical, "גבוהה", "קריטית");
        Order(r => r.Medium, r => r.High, "בינונית", "גבוהה");
        Order(r => r.Low, r => r.Medium, "נמוכה", "בינונית");
    }

    public static string OrderMessage(string level, string moreUrgentLevel) =>
        $"היעד לדחיפות {level} לא יכול להיות קצר מהיעד לדחיפות {moreUrgentLevel}";

    private void Hours(System.Linq.Expressions.Expression<Func<UpdateSlaPoliciesRequest, int?>> level, string label) =>
        RuleFor(level).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage($"יש להזין יעד לדחיפות {label}")
            .InclusiveBetween(1, MaxHours).WithMessage($"דחיפות {label}: מספר שעות שלם בין 1 ל-{MaxHours}");

    private void Order(System.Linq.Expressions.Expression<Func<UpdateSlaPoliciesRequest, int?>> level,
        Func<UpdateSlaPoliciesRequest, int?> moreUrgent, string label, string moreUrgentLabel)
    {
        var compiled = level.Compile();
        RuleFor(level)
            .Must((request, hours) => hours >= moreUrgent(request))
            .WithMessage(OrderMessage(label, moreUrgentLabel))
            // Only between two valid values: a missing or out-of-range level already has its own message.
            .When(r => compiled(r) is >= 1 and <= MaxHours && moreUrgent(r) is >= 1 and <= MaxHours);
    }
}
