using System.Globalization;
using Common.Constants;
using Common.DTOs.Ai.Evaluation;
using FluentValidation;

namespace Services.Validators;

/// <summary>
/// POST /api/dev/ai/analyze (development only). Same maximum lengths as a ticket; no minimum, so very short texts can
/// be tried.
/// </summary>
public sealed class DevAiAnalyzeRequestValidator : AbstractValidator<DevAiAnalyzeRequest>
{
    public static readonly string TextLengthMessage =
        $"הטקסט: עד {TicketFieldLimits.DescriptionMaxLength.ToString("N0", CultureInfo.InvariantCulture)} תווים";

    public DevAiAnalyzeRequestValidator()
    {
        RuleFor(r => r.Subject)
            .Must(subject => subject is null || subject.Trim().Length <= TicketFieldLimits.SubjectMaxLength)
            .WithMessage(CreateTicketRequestValidator.SubjectLengthMessage);

        RuleFor(r => r.Text)
            .Cascade(CascadeMode.Stop)
            .Must(text => !string.IsNullOrWhiteSpace(text)).WithMessage("יש להזין טקסט לניתוח")
            .Must(text => text.Trim().Length <= TicketFieldLimits.DescriptionMaxLength)
            .WithMessage(TextLengthMessage);
    }
}
