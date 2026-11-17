using Common.DTOs.Dashboard;
using FluentValidation;

namespace Services.Validators;

/// <summary>The date range of the dashboards (spec 7.9): from ≤ to, at most a year. A missing day is derived (30 days).</summary>
public abstract class DashboardRangeQueryValidator<T> : AbstractValidator<T> where T : DashboardRangeQuery
{
    public const string FromAfterToMessage = "תאריך ההתחלה חייב להיות לפני תאריך הסיום או שווה לו";

    public static readonly string RangeTooLongMessage = $"טווח התאריכים: עד {DashboardLimits.MaxRangeDays} ימים";

    protected DashboardRangeQueryValidator()
    {
        RuleFor(q => q.From)
            .Must((q, from) => from is null || q.To is null || from <= q.To)
            .WithMessage(FromAfterToMessage);

        RuleFor(q => q.To)
            .Must((q, to) => q.From is null || to is null || q.From > to
                             || to.Value.DayNumber - q.From.Value.DayNumber + 1 <= DashboardLimits.MaxRangeDays)
            .WithMessage(RangeTooLongMessage);
    }
}

public sealed class DepartmentDashboardQueryValidator : DashboardRangeQueryValidator<DepartmentDashboardQuery>
{
    public DepartmentDashboardQueryValidator()
    {
        RuleFor(q => q.View).IsInEnum().WithMessage("התצוגה אינה קיימת (requesters או handling)");
    }
}

public sealed class OrganizationDashboardQueryValidator : DashboardRangeQueryValidator<OrganizationDashboardQuery>;
