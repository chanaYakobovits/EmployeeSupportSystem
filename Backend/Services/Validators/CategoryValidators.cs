using System.Text.RegularExpressions;
using Common.DTOs.Categories;
using Common.Enums;
using FluentValidation;

namespace Services.Validators;

/// <summary>Rules shared by create and update (spec 3.5, 8.2). Lengths as in CategoryConfiguration.</summary>
public sealed partial class CategoryFieldsValidator : AbstractValidator<ICategoryFields>
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    public const string CodeFormatMessage = "קוד: אותיות אנגליות גדולות וקו תחתון בלבד, מתחיל באות (2 עד 30 תווים)";
    public const string HandlingDepartmentRequiredMessage = "יש לבחור מחלקה מטפלת";
    public const string HandlingDepartmentNotAllowedMessage = "בניתוב ל-HR של מחלקת העובד אין מחלקה מטפלת";

    public CategoryFieldsValidator()
    {
        RuleFor(r => r.Name).RequiredText("יש להזין שם קטגוריה", "שם קטגוריה", NameMaxLength);
        RuleFor(r => r.Description).RequiredText("יש להזין תיאור", "תיאור", DescriptionMaxLength);
        RuleFor(r => r.RoutingType).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("יש לבחור סוג ניתוב")
            .IsInEnum().WithMessage("סוג הניתוב אינו תקין");

        RuleFor(r => r.HandlingDepartmentId)
            .Must(id => id is > 0).WithMessage(HandlingDepartmentRequiredMessage)
            .When(r => r.RoutingType == RoutingType.FixedDepartment);
        RuleFor(r => r.HandlingDepartmentId)
            .Null().WithMessage(HandlingDepartmentNotAllowedMessage)
            .When(r => r.RoutingType == RoutingType.RequesterDepartmentHr);
    }

    public static bool IsValidCode(string code) => CodePattern().IsMatch(code);

    // Upper-case English letters and underscores, starting with a letter; 2–30 characters (e.g. PAYROLL, HOME_OFFICE).
    [GeneratedRegex("^[A-Z][A-Z_]{1,29}$")]
    private static partial Regex CodePattern();
}

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(r => r.Code).Cascade(CascadeMode.Stop)
            .Must(code => !string.IsNullOrWhiteSpace(code)).WithMessage("יש להזין קוד")
            .Must(code => CategoryFieldsValidator.IsValidCode(code.Trim()))
            .WithMessage(CategoryFieldsValidator.CodeFormatMessage);
        Include(new CategoryFieldsValidator());
    }
}

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        Include(new CategoryFieldsValidator());
        RuleFor(r => r.IsActive).NotNull().WithMessage("יש לציין אם הקטגוריה פעילה");
    }
}
