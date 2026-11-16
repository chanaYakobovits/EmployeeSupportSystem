using Common.Constants;
using Common.DTOs.Tickets;
using Common.Enums;
using FluentValidation;

namespace Services.Validators;

/// <summary>The list of tickets of a bulk request (task 3.6): 1–100 tickets, each with an id and a version, no repeats.</summary>
internal static class BulkItemRules
{
    public const string ItemsRequiredMessage = "יש לבחור לפחות פנייה אחת";
    public static readonly string TooManyMessage = $"ניתן לעדכן עד {BulkLimits.MaxItems} פניות בפעולה אחת";
    public const string TicketIdMessage = "מספר הפנייה אינו תקין";

    public static string DuplicateMessage(int id) => $"פנייה מספר {id} נבחרה יותר מפעם אחת";

    public static void AddRules<T>(AbstractValidator<T> validator, Func<T, IReadOnlyList<BulkTicketItem>?> items)
    {
        validator.RuleFor(r => items(r))
            .Cascade(CascadeMode.Stop)
            .Must(list => list is { Count: > 0 }).WithMessage(ItemsRequiredMessage)
            .Must(list => list!.Count <= BulkLimits.MaxItems).WithMessage(TooManyMessage)
            .Must(list => FirstDuplicate(list!) is null)
            .WithMessage((_, list) => DuplicateMessage(FirstDuplicate(list!)!.Value))
            .OverridePropertyName("items");

        validator.RuleForEach(r => items(r))
            .ChildRules(item =>
            {
                item.RuleFor(i => i.Id).NotNull().WithMessage(TicketIdMessage)
                    .GreaterThan(0).WithMessage(TicketIdMessage);
                item.RuleFor(i => i.RowVersion).Must(HandlingRules.IsRowVersion)
                    .WithMessage(HandlingRules.RowVersionMessage);
            })
            .When(r => items(r) is { Count: > 0 and <= BulkLimits.MaxItems })
            .OverridePropertyName("items");
    }

    private static int? FirstDuplicate(IReadOnlyList<BulkTicketItem> list) =>
        list.Where(i => i.Id is not null)
            .GroupBy(i => i.Id!.Value)
            .Where(g => g.Count() > 1)
            .Select(g => (int?)g.Key)
            .FirstOrDefault();
}

/// <summary>POST /api/tickets/bulk/status (spec 11.2): "התחל טיפול" only.</summary>
public sealed class BulkChangeStatusRequestValidator : AbstractValidator<BulkChangeStatusRequest>
{
    public const string StartOnlyMessage =
        "בעדכון מרובה אפשר רק להתחיל טיפול. בקשת מידע ופתרון נשלחים עם הודעה לעובד, לכל פנייה בנפרד";

    public BulkChangeStatusRequestValidator()
    {
        RuleFor(r => r.NewStatus)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(StartOnlyMessage)
            .Equal(TicketStatus.InProgress).WithMessage(StartOnlyMessage);

        BulkItemRules.AddRules(this, r => r.Items);
    }
}

/// <summary>POST /api/tickets/bulk/assign: one target (as POST /{id}/assign) and one required reason for every ticket.</summary>
public sealed class BulkAssignRequestValidator : AbstractValidator<BulkAssignRequest>
{
    public BulkAssignRequestValidator()
    {
        RuleFor(r => r.TargetUserId)
            .Must((r, userId) => (userId is null) != (r.TargetDepartmentId is null))
            .WithMessage(AssignTicketRequestValidator.OneTargetMessage)
            .GreaterThan(0).When(r => r.TargetUserId is not null, ApplyConditionTo.CurrentValidator)
            .WithMessage("המטפל אינו קיים");

        RuleFor(r => r.TargetDepartmentId)
            .GreaterThan(0).When(r => r.TargetDepartmentId is not null)
            .WithMessage("המחלקה אינה קיימת");

        RuleFor(r => r.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason)).WithMessage(HandlingRules.ReasonRequiredMessage)
            .Must(reason => HandlingRules.HasValidLength(reason!)).WithMessage(HandlingRules.ReasonLengthMessage);

        BulkItemRules.AddRules(this, r => r.Items);
    }
}
