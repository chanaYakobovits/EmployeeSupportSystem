using System.Globalization;
using Common.Constants;
using Common.DTOs.Tickets;
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
