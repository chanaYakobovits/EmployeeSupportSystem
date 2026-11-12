namespace Common.Constants;

/// <summary>Employee code format (spec 4.2): "EMP-" + 5 digits, e.g. EMP-00042. Used by the service and the seeder.</summary>
public static class EmployeeCodeFormat
{
    public const string Prefix = "EMP-";

    public static string Format(long number) => $"{Prefix}{number:D5}";
}
