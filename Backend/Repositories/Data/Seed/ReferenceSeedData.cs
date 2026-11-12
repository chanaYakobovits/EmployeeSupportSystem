using Common.Enums;

namespace Repositories.Data.Seed;

internal sealed record CategorySeed(
    string Code,
    string Name,
    string Description,
    RoutingType RoutingType,
    DepartmentKey? HandlingDepartment,
    bool IsFallback = false);

/// <summary>Categories and routing (spec 3.5). SLA targets: <see cref="Common.Constants.SlaDefaults"/>.</summary>
internal static class ReferenceSeedData
{
    public const string Payroll = "PAYROLL";
    public const string Attendance = "ATTENDANCE";
    public const string Leave = "LEAVE";
    public const string Equipment = "EQUIPMENT";
    public const string Admin = "ADMIN";
    public const string Benefits = "BENEFITS";
    public const string Training = "TRAINING";
    public const string General = "GENERAL";

    public static readonly CategorySeed[] Categories =
    [
        new(Payroll, "שכר ותלושים", "טעות בתלוש, שעות נוספות שלא שולמו",
            RoutingType.FixedDepartment, DepartmentKey.Accounting),
        new(Attendance, "נוכחות ושעות", "שכחתי להחתים, תיקון דיווח",
            RoutingType.RequesterDepartmentHr, null),
        new(Leave, "חופשות והיעדרויות", "יתרת חופשה, בקשת חופשה",
            RoutingType.RequesterDepartmentHr, null),
        new(Equipment, "ציוד ומחשוב", "מחשב איטי, סיסמה, מסך נוסף",
            RoutingType.FixedDepartment, DepartmentKey.InformationSystems),
        new(Admin, "בקשות מנהלתיות", "אישור העסקה, חניה, כרטיס כניסה",
            RoutingType.FixedDepartment, DepartmentKey.Secretariat),
        new(Benefits, "רווחה והטבות", "שובר חג, הטבות עובדים",
            RoutingType.FixedDepartment, DepartmentKey.HumanResources),
        new(Training, "הדרכה והשתלמויות", "בקשה לקורס",
            RoutingType.FixedDepartment, DepartmentKey.HumanResources),
        new(General, "כללי", "תור גיבוי – כל מה שלא סווג",
            RoutingType.FixedDepartment, DepartmentKey.HumanResources, IsFallback: true)
    ];
}
