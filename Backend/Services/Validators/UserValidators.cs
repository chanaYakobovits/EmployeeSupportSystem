using Common.DTOs.Users;
using FluentValidation;

namespace Services.Validators;

public sealed class UserListQueryValidator : PageQueryValidator<UserListQuery>
{
    public const int MaxSearchLength = 100;

    public UserListQueryValidator()
        : base(UserSortFields.All)
    {
        RuleFor(q => q.Search)
            .Must(search => search is null || search.Trim().Length <= MaxSearchLength)
            .WithMessage($"טקסט החיפוש: עד {MaxSearchLength} תווים");

        RuleFor(q => q.DepartmentId)
            .GreaterThan(0).When(q => q.DepartmentId is not null)
            .WithMessage("מזהה המחלקה אינו תקין");

        RuleFor(q => q.Role).IsInEnum().WithMessage("התפקיד אינו קיים");
        RuleFor(q => q.InvitationStatus).IsInEnum().WithMessage("סטטוס ההזמנה אינו קיים");
    }
}

/// <summary>Field limits match UserConfiguration (database column lengths).</summary>
public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(r => r.FirstName).RequiredText("יש להזין שם פרטי", "שם פרטי", 50);
        RuleFor(r => r.LastName).RequiredText("יש להזין שם משפחה", "שם משפחה", 50);
        RuleFor(r => r.Email).ValidEmail();
        RuleFor(r => r.DepartmentId).GreaterThan(0).WithMessage("יש לבחור מחלקה");
        RuleFor(r => r.JobTitle).RequiredText("יש להזין תפקיד בארגון", "תפקיד בארגון", 100);
        RuleForEach(r => r.Roles).IsInEnum().WithMessage("התפקיד אינו קיים");
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(r => r.FirstName).RequiredText("יש להזין שם פרטי", "שם פרטי", 50);
        RuleFor(r => r.LastName).RequiredText("יש להזין שם משפחה", "שם משפחה", 50);
        RuleFor(r => r.Email).ValidEmail();
        RuleFor(r => r.DepartmentId).GreaterThan(0).WithMessage("יש לבחור מחלקה");
        RuleFor(r => r.JobTitle).RequiredText("יש להזין תפקיד בארגון", "תפקיד בארגון", 100);
        RuleForEach(r => r.Roles).IsInEnum().WithMessage("התפקיד אינו קיים");
    }
}

public sealed class SetUserActiveRequestValidator : AbstractValidator<SetUserActiveRequest>
{
    public SetUserActiveRequestValidator()
    {
        RuleFor(r => r.IsActive).NotNull().WithMessage("יש לציין אם המשתמש פעיל");
    }
}
