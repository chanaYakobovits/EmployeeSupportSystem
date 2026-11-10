namespace Services.Interfaces;

/// <summary>
/// Kinds of personal data replaced by <see cref="IPiiRedactor"/> (spec 5.3).
/// </summary>
public enum PiiType
{
    Name,
    Email,
    Phone,
    IdNumber,
    Number,
    EmployeeCode
}
