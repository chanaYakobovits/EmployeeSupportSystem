using Common.DTOs.Auth;
using FluentValidation;
using Services.Implementations;
using Services.Implementations.Invitations;

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

/// <summary>
/// Presence and length only; the password policy is checked by InvitationService. A missing or oversized token gets
/// the same message as any other invalid link (a real token is 43 characters).
/// </summary>
public sealed class SetPasswordRequestValidator : AbstractValidator<SetPasswordRequest>
{
    public const int MaxTokenLength = 100;

    public SetPasswordRequestValidator()
    {
        RuleFor(r => r.Token).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(InvitationService.InvalidLinkMessage)
            .MaximumLength(MaxTokenLength).WithMessage(InvitationService.InvalidLinkMessage);
        RuleFor(r => r.NewPassword).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("יש להזין סיסמה חדשה")
            .MaximumLength(256).WithMessage("סיסמה: עד 256 תווים");
    }
}
