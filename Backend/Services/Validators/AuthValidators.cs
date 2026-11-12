using Common.DTOs.Auth;
using FluentValidation;
using Services.Implementations;

namespace Services.Validators;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Identifier).RequiredText("יש להזין מייל או קוד עובד", "מייל או קוד עובד", 256);
        // Not trimmed: a password may contain spaces. Only presence and an upper bound (hashing cost).
        RuleFor(r => r.Password).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("יש להזין סיסמה")
            .MaximumLength(PasswordPolicy.MaxLength).WithMessage($"סיסמה: עד {PasswordPolicy.MaxLength} תווים");
    }
}

/// <summary>Presence and length only; the password policy (spec 6.1) is checked by AuthService.</summary>
public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(r => r.CurrentPassword).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("יש להזין את הסיסמה הנוכחית")
            .MaximumLength(PasswordPolicy.MaxLength).WithMessage($"סיסמה: עד {PasswordPolicy.MaxLength} תווים");
        // Longer values pass here so the policy can report "too long" in its own words.
        RuleFor(r => r.NewPassword).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("יש להזין סיסמה חדשה")
            .MaximumLength(256).WithMessage("סיסמה: עד 256 תווים");
    }
}
