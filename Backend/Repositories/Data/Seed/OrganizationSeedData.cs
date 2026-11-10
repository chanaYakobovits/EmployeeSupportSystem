using Common.Enums;

namespace Repositories.Data.Seed;

/// <summary>Keys of the demo departments (spec 2.2).</summary>
internal enum DepartmentKey
{
    Management,
    Development,
    Accounting,
    Secretariat,
    InformationSystems,
    HumanResources
}

internal sealed record DepartmentSeed(DepartmentKey Key, string Name);

/// <summary>A demo person. All names are fictional.</summary>
internal sealed record PersonSeed(
    string FirstName,
    string LastName,
    string EmailLocalPart,
    string JobTitle,
    DepartmentKey Department,
    Role[] ExtraRoles,
    bool IsDepartmentManager = false,
    bool IsDepartmentHr = false,
    bool IsActive = true,
    bool MustChangePassword = false);

/// <summary>
/// The demo organization from spec 2.2, completed to 25 people (spec 8.5) with regular, non-handler employees.
/// Order matters: the position in <see cref="People"/> becomes the employee code (EMP-00001 ...).
/// </summary>
internal static class OrganizationSeedData
{
    public const string OrganizationName = "משרד לדוגמה בע\"מ";
    public const string EmailDomain = "example.com";

    /// <summary>The single known password of all demo users (documented in README).</summary>
    public const string DemoPassword = "Demo1234";

    public static readonly DepartmentSeed[] Departments =
    [
        new(DepartmentKey.Management, "הנהלה"),
        new(DepartmentKey.Development, "פיתוח"),
        new(DepartmentKey.Accounting, "הנהלת חשבונות"),
        new(DepartmentKey.Secretariat, "מזכירות ומנהלה"),
        new(DepartmentKey.InformationSystems, "מערכות מידע"),
        new(DepartmentKey.HumanResources, "משאבי אנוש מרכזי")
    ];

    private static readonly Role[] None = [];
    private static readonly Role[] Handler = [Role.Handler];
    private static readonly Role[] Manager = [Role.DepartmentManager];

    public static readonly PersonSeed[] People =
    [
        // Management
        new("אבנר", "גולדמן", "avner.goldman", "מנכ\"ל", DepartmentKey.Management, [Role.Executive], IsDepartmentManager: true),
        new("שירה", "אלמוג", "shira.almog", "עוזרת מנכ\"ל", DepartmentKey.Management, None),

        // Development: manager, 6 developers, departmental HR, 2 QA (one has left)
        new("רון", "כהנוביץ", "ron.kahanovich", "מנהל פיתוח", DepartmentKey.Development, Manager, IsDepartmentManager: true),
        new("נועה", "ברקוביץ", "noa.berkovich", "מפתחת בכירה", DepartmentKey.Development, None),
        new("עידו", "שגיא", "ido.sagi", "מפתח", DepartmentKey.Development, None),
        new("מיכל", "רוזנטל", "michal.rosenthal", "מפתחת", DepartmentKey.Development, None),
        new("יונתן", "אורן", "yonatan.oren", "מפתח", DepartmentKey.Development, None),
        new("הדר", "פלד", "hadar.peled", "מפתחת", DepartmentKey.Development, None),
        new("תומר", "זילבר", "tomer.zilber", "מפתח", DepartmentKey.Development, None, MustChangePassword: true),
        new("ליאת", "שחר", "liat.shahar", "רכזת משאבי אנוש מחלקתית", DepartmentKey.Development, Handler, IsDepartmentHr: true),
        new("גיל", "נחמיאס", "gil.nahmias", "בודק תוכנה", DepartmentKey.Development, None),
        new("אורית", "דנון", "orit.danon", "בודקת תוכנה", DepartmentKey.Development, None, IsActive: false),

        // Accounting: chief accountant, 3 accountants (payroll handlers for the whole organization), payroll clerk
        new("רינה", "קפלן", "rina.kaplan", "מנהלת חשבונות ראשית", DepartmentKey.Accounting, Manager, IsDepartmentManager: true),
        new("אלון", "ביטון", "alon.biton", "מנהל חשבונות", DepartmentKey.Accounting, Handler),
        new("סיגל", "מרקוביץ", "sigal.markovich", "מנהלת חשבונות", DepartmentKey.Accounting, Handler),
        new("עמית", "טל", "amit.tal", "מנהל חשבונות", DepartmentKey.Accounting, Handler),
        new("דנה", "יעקובי", "dana.yaakobi", "פקידת שכר", DepartmentKey.Accounting, None),

        // Secretariat: manager + 2 secretaries (administrative requests for the whole organization)
        new("אסנת", "חביב", "osnat.haviv", "מנהלת מזכירות", DepartmentKey.Secretariat, Manager, IsDepartmentManager: true),
        new("רותם", "לביא", "rotem.lavi", "מזכירה", DepartmentKey.Secretariat, Handler),
        new("מורן", "אשכנזי", "moran.ashkenazi", "מזכירה", DepartmentKey.Secretariat, Handler),

        // Information systems: manager, technician (equipment for the whole organization), network administrator
        new("אריאל", "פרידמן", "ariel.friedman", "מנהל מערכות מידע", DepartmentKey.InformationSystems, Manager, IsDepartmentManager: true),
        new("שי", "גבאי", "shai.gabay", "טכנאי מחשבים", DepartmentKey.InformationSystems, Handler),
        new("אלעד", "ברנע", "elad.barnea", "אחראי רשתות", DepartmentKey.InformationSystems, None),

        // Central HR: HR manager = system admin + handler (spec 14, question 10) + manager of the HR department; HR coordinator
        new("יעל", "הרפז", "yael.harpaz", "מנהלת משאבי אנוש", DepartmentKey.HumanResources,
            [Role.Admin, Role.Handler, Role.DepartmentManager], IsDepartmentManager: true),
        new("עדי", "שמעוני", "adi.shimoni", "רכזת משאבי אנוש", DepartmentKey.HumanResources, Handler)
    ];

    public static string EmployeeCode(int position) => $"EMP-{position:D5}";
}
