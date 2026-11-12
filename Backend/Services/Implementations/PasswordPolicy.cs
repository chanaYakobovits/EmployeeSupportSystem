namespace Services.Implementations;

/// <summary>
/// Spec 6.1: at least 8 characters, a letter and a digit. Any script counts as a letter (Hebrew too).
/// Called by AuthService after the request validator. TemporaryPasswordGenerator always satisfies it.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public const string TooShort = "הסיסמה חייבת להכיל לפחות 8 תווים";
    public const string TooLong = "הסיסמה יכולה להכיל עד 128 תווים";
    public const string MissingLetter = "הסיסמה חייבת להכיל לפחות אות אחת";
    public const string MissingDigit = "הסיסמה חייבת להכיל לפחות ספרה אחת";
    public const string SameAsCurrent = "הסיסמה החדשה חייבת להיות שונה מהסיסמה הנוכחית";

    /// <returns>User-facing (Hebrew) errors; empty when the password is acceptable.</returns>
    public static IReadOnlyList<string> Validate(string newPassword, string? currentPassword = null)
    {
        var errors = new List<string>();

        if (newPassword.Length < MinLength)
        {
            errors.Add(TooShort);
        }

        if (newPassword.Length > MaxLength)
        {
            errors.Add(TooLong);
        }

        if (!newPassword.Any(char.IsLetter))
        {
            errors.Add(MissingLetter);
        }

        if (!newPassword.Any(char.IsDigit))
        {
            errors.Add(MissingDigit);
        }

        if (currentPassword is not null && string.Equals(newPassword, currentPassword, StringComparison.Ordinal))
        {
            errors.Add(SameAsCurrent);
        }

        return errors;
    }
}
