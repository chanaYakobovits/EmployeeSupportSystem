using Common.DTOs.Departments;
using FluentValidation;

namespace Services.Validators;

public sealed class CreateDepartmentRequestValidator : AbstractValidator<CreateDepartmentRequest>
{
    public CreateDepartmentRequestValidator()
    {
        RuleFor(r => r.Name).RequiredText("יש להזין שם מחלקה", "שם מחלקה", 100);
        RuleFor(r => r.ManagerUserId).GreaterThan(0).When(r => r.ManagerUserId is not null)
            .WithMessage("יש לבחור עובד פעיל בארגון");
    }
}

public sealed class UpdateDepartmentRequestValidator : AbstractValidator<UpdateDepartmentRequest>
{
    public UpdateDepartmentRequestValidator()
    {
        RuleFor(r => r.Name).RequiredText("יש להזין שם מחלקה", "שם מחלקה", 100);
        RuleFor(r => r.ManagerUserId).GreaterThan(0).When(r => r.ManagerUserId is not null)
            .WithMessage("יש לבחור עובד פעיל בארגון");
        RuleFor(r => r.IsActive).NotNull().WithMessage("יש לציין אם המחלקה פעילה");
    }
}
