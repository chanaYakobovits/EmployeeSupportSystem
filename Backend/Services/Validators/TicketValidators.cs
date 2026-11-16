using System.Globalization;
using Common.Constants;
using Common.DTOs.Tickets;
using Common.Enums;
using FluentValidation;

namespace Services.Validators;

/// <summary>Spec 3.1, 7.4. Lengths are counted after trimming, as the text is stored trimmed.</summary>
public sealed class CreateTicketRequestValidator : AbstractValidator<CreateTicketRequest>
{
    public const string DescriptionRequiredMessage = "יש לתאר את הפנייה";

    public static readonly string SubjectLengthMessage = $"נושא: עד {TicketFieldLimits.SubjectMaxLength} תווים";

    public static readonly string DescriptionLengthMessage =
        $"תיאור הפנייה: בין {TicketFieldLimits.DescriptionMinLength} ל-{TicketFieldLimits.DescriptionMaxLength.ToString("N0", CultureInfo.InvariantCulture)} תווים";

    public CreateTicketRequestValidator()
    {
        RuleFor(r => r.Subject)
            .Must(subject => subject is null || subject.Trim().Length <= TicketFieldLimits.SubjectMaxLength)
            .WithMessage(SubjectLengthMessage);

        RuleFor(r => r.Description)
            .Cascade(CascadeMode.Stop)
            .Must(description => !string.IsNullOrWhiteSpace(description)).WithMessage(DescriptionRequiredMessage)
            .Must(description => description.Trim().Length is >= TicketFieldLimits.DescriptionMinLength
                and <= TicketFieldLimits.DescriptionMaxLength)
            .WithMessage(DescriptionLengthMessage);
    }
}

public sealed class MyTicketsQueryValidator : PageQueryValidator<MyTicketsQuery>
{
    public const int MaxSearchLength = 100;

    public MyTicketsQueryValidator()
        : base(MyTicketSortFields.All)
    {
        RuleFor(q => q.Search)
            .Must(search => search is null || search.Trim().Length <= MaxSearchLength)
            .WithMessage($"טקסט החיפוש: עד {MaxSearchLength} תווים");

        RuleForEach(q => q.Status).IsInEnum().WithMessage("הסטטוס אינו קיים");
    }
}

/// <summary>Shared rules of the handling requests (task 3.4).</summary>
internal static class HandlingRules
{
    public static readonly string ReasonLengthMessage =
        $"נימוק: בין {TicketFieldLimits.ReasonMinLength} ל-{TicketFieldLimits.ReasonMaxLength} תווים";

    public const string ReasonRequiredMessage = "יש לכתוב נימוק";

    public const string RowVersionMessage = "חסרה גרסת הפנייה. יש לרענן את הפנייה ולנסות שוב";

    /// <summary>A rowversion is 8 bytes; anything else cannot be a version the client received.</summary>
    public static bool IsRowVersion(byte[]? rowVersion) => rowVersion is { Length: 8 };

    public static bool HasValidLength(string reason) =>
        reason.Trim().Length is >= TicketFieldLimits.ReasonMinLength and <= TicketFieldLimits.ReasonMaxLength;
}

/// <summary>PATCH /api/tickets/{id}/classification (spec 4.4, 9.2).</summary>
public sealed class ReclassifyTicketRequestValidator : AbstractValidator<ReclassifyTicketRequest>
{
    public const string NothingToChangeMessage = "יש לבחור קטגוריה או רמת דחיפות";

    public ReclassifyTicketRequestValidator()
    {
        RuleFor(r => r.CategoryId)
            .Must((r, categoryId) => categoryId is not null || r.Urgency is not null)
            .WithMessage(NothingToChangeMessage)
            .GreaterThan(0).When(r => r.CategoryId is not null, ApplyConditionTo.CurrentValidator)
            .WithMessage("הקטגוריה אינה קיימת");

        RuleFor(r => r.Urgency).IsInEnum().WithMessage("רמת הדחיפות אינה קיימת");

        RuleFor(r => r.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason)).WithMessage(HandlingRules.ReasonRequiredMessage)
            .Must(HandlingRules.HasValidLength).WithMessage(HandlingRules.ReasonLengthMessage);

        RuleFor(r => r.RowVersion).Must(HandlingRules.IsRowVersion).WithMessage(HandlingRules.RowVersionMessage);
    }
}

/// <summary>
/// POST /api/tickets/{id}/assign (spec 4.4, 9.2). Whether the reason is required depends on the ticket (not when a
/// handler takes it from the queue) – checked by the service.
/// </summary>
public sealed class AssignTicketRequestValidator : AbstractValidator<AssignTicketRequest>
{
    public const string OneTargetMessage = "יש לבחור מטפל או מחלקה (אחד מהם)";

    public AssignTicketRequestValidator()
    {
        RuleFor(r => r.TargetUserId)
            .Must((r, userId) => (userId is null) != (r.TargetDepartmentId is null))
            .WithMessage(OneTargetMessage)
            .GreaterThan(0).When(r => r.TargetUserId is not null, ApplyConditionTo.CurrentValidator)
            .WithMessage("המטפל אינו קיים");

        RuleFor(r => r.TargetDepartmentId)
            .GreaterThan(0).When(r => r.TargetDepartmentId is not null)
            .WithMessage("המחלקה אינה קיימת");

        RuleFor(r => r.Reason!)
            .Must(HandlingRules.HasValidLength).When(r => !string.IsNullOrWhiteSpace(r.Reason))
            .WithMessage(HandlingRules.ReasonLengthMessage);

        RuleFor(r => r.RowVersion).Must(HandlingRules.IsRowVersion).WithMessage(HandlingRules.RowVersionMessage);
    }
}

/// <summary>GET /api/tickets – the staff lists (spec 9.4).</summary>
public sealed class StaffTicketsQueryValidator : PageQueryValidator<StaffTicketsQuery>
{
    public const int MaxSearchLength = MyTicketsQueryValidator.MaxSearchLength;
    public const string ScopeRequiredMessage = "יש לבחור רשימה (scope): assigned, queue, handling או department";
    public const string DateRangeMessage = "תאריך הסיום חייב להיות באותו יום או אחרי תאריך ההתחלה";

    public StaffTicketsQueryValidator()
        : base(StaffTicketSortFields.All)
    {
        RuleFor(q => q.Scope)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(ScopeRequiredMessage)
            .IsInEnum().WithMessage(ScopeRequiredMessage);

        RuleForEach(q => q.Status).IsInEnum().WithMessage("הסטטוס אינו קיים");

        RuleForEach(q => q.Urgency).IsInEnum().WithMessage("רמת הדחיפות אינה קיימת");

        RuleFor(q => q.CategoryId)
            .GreaterThan(0).When(q => q.CategoryId is not null)
            .WithMessage("הקטגוריה אינה קיימת");

        RuleFor(q => q.To)
            .Must((q, to) => q.From is null || to is null || q.From <= to)
            .WithMessage(DateRangeMessage);

        RuleFor(q => q.Search)
            .Must(search => search is null || search.Trim().Length <= MaxSearchLength)
            .WithMessage($"טקסט החיפוש: עד {MaxSearchLength} תווים");
    }
}

/// <summary>
/// PATCH /api/tickets/{id}/status (spec 3.3, 9.2, task 3.5). Whether the transition is allowed, and whether a reason is
/// allowed at all (handlers only), depends on the ticket and the user – checked by the service.
/// </summary>
public sealed class ChangeTicketStatusRequestValidator : AbstractValidator<ChangeTicketStatusRequest>
{
    public const string StatusRequiredMessage = "יש לבחור את הסטטוס החדש";
    public const string InfoRequestRequiredMessage = "יש לכתוב לעובד איזה מידע חסר";
    public const string ResolutionRequiredMessage = "יש לכתוב לעובד את הפתרון";

    public static readonly string MessageLengthMessage =
        $"ההודעה: עד {CommentFieldLimits.BodyMaxLength.ToString("N0", CultureInfo.InvariantCulture)} תווים";

    public ChangeTicketStatusRequestValidator()
    {
        RuleFor(r => r.NewStatus)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(StatusRequiredMessage)
            .IsInEnum().WithMessage("הסטטוס אינו קיים");

        // Spec 3.3: "נפתרה" requires the reply to the employee; asking for information requires saying what is missing.
        RuleFor(r => r.Message)
            .Cascade(CascadeMode.Stop)
            .Must((r, message) => !string.IsNullOrWhiteSpace(message)
                                  || r.NewStatus is not (TicketStatus.WaitingForEmployee or TicketStatus.Resolved))
            .WithMessage(r => r.NewStatus is TicketStatus.Resolved
                ? ResolutionRequiredMessage
                : InfoRequestRequiredMessage)
            .Must(message => message is null || message.Trim().Length <= CommentFieldLimits.BodyMaxLength)
            .WithMessage(MessageLengthMessage);

        RuleFor(r => r.Reason!)
            .Must(HandlingRules.HasValidLength).When(r => !string.IsNullOrWhiteSpace(r.Reason))
            .WithMessage(HandlingRules.ReasonLengthMessage);

        RuleFor(r => r.RowVersion).Must(HandlingRules.IsRowVersion).WithMessage(HandlingRules.RowVersionMessage);
    }
}
