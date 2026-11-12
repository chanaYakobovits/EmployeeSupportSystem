using System.Text.RegularExpressions;
using FluentValidation;

namespace Services.Validators;

/// <summary>Shared rules with Hebrew messages. Each rule stops at its first failure (one message per field).</summary>
internal static partial class RuleBuilderExtensions
{
    /// <summary>Not null / empty / whitespace, and at most <paramref name="maxLength"/> characters after trimming.</summary>
    /// <param name="requiredMessage">E.g. "יש להזין שם פרטי".</param>
    /// <param name="label">E.g. "שם פרטי", for the length message.</param>
    public static IRuleBuilderOptions<T, string> RequiredText<T>(this IRuleBuilderInitial<T, string> rule,
        string requiredMessage, string label, int maxLength) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage(requiredMessage)
            .Must(value => value.Trim().Length <= maxLength).WithMessage($"{label}: עד {maxLength} תווים");

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.RequiredText("יש להזין כתובת מייל", "כתובת מייל", 256)
            .Must(value => EmailPattern().IsMatch(value.Trim())).WithMessage("כתובת המייל אינה תקינה");

    // Pragmatic: something@domain.tld, no spaces, one @. Real verification is the employee signing in.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}
