# עובדנט – EmployeeSupportSystem

מערכת לניהול פניות עובדים. האפיון המלא נמצא ב-`docs/`.

## דרישות מקדימות

- .NET SDK 9
- SQL Server Express (המופע `.\SQLEXPRESS`)
- Node.js ו-Angular CLI (בשביל ה-Frontend)

## הגדרה ראשונית (Backend)

מחרוזת החיבור נשמרת רק ב-user-secrets של פרויקט API, ולעולם לא ב-`appsettings.json` (סעיף 6.5).
את כל הפקודות מריצים מתוך התיקייה `Backend/`:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.\SQLEXPRESS;Database=EmployeeSupportDB;Trusted_Connection=True;TrustServerCertificate=True" --project API
dotnet ef database update --project Repositories --startup-project API
```

## נתוני דוגמה (Seeder)

```bash
dotnet run --project API -- --seed
```

> ⚠️ **הפקודה מוחקת את המסד `EmployeeSupportDB` ויוצרת אותו מחדש** מה-Migrations, ואחר כך ממלאת אותו בנתוני דוגמה.
> היא רצה רק בסביבת `Development`. בכל סביבה אחרת היא מסרבת ויוצאת עם קוד 1.
> הפעלה רגילה של ה-API, בלי `--seed`, לא נוגעת בנתונים.

התוצאה זהה בכל הרצה, כי ה-Random מאותחל עם seed קבוע.
כל המועדים יחסיים לרגע ההרצה, כך שהפניות תמיד פרוסות על 60 הימים האחרונים, וחלק מהפניות הפתוחות תמיד בחריגת SLA.

**מה נוצר:**

- ארגון אחד, "משרד לדוגמה בע"מ".
- 6 מחלקות ו-25 משתמשים, עם כל התפקידים.
- 8 קטגוריות עם כללי הניתוב.
- מדיניות SLA.
- 200 פניות בעברית, בכל הסטטוסים, יחד עם היסטוריית סטטוסים, תגובות וניתוחי AI.

## משתמשי דוגמה

**הסיסמה של כל משתמשי הדוגמה: `Demo1234`**

אפשר להתחבר עם המייל או עם קוד העובד. השמות בדויים.

| תפקיד במערכת | שם | קוד עובד | מייל | הערות |
|---|---|---|---|---|
| Employee | נועה ברקוביץ | EMP-00004 | noa.berkovich@example.com | מפתחת. רואה רק את הפניות שלה |
| Handler | אלון ביטון | EMP-00014 | alon.biton@example.com | הנהלת חשבונות, מטפל בשכר לכל הארגון |
| Handler (HR מחלקתי) | ליאת שחר | EMP-00010 | liat.shahar@example.com | מטפלת בנוכחות ובחופשות של עובדי הפיתוח |
| DepartmentManager | רון כהנוביץ | EMP-00003 | ron.kahanovich@example.com | מנהל הפיתוח |
| DepartmentManager | רינה קפלן | EMP-00013 | rina.kaplan@example.com | הנהלת חשבונות, מחלקה מטפלת (שתי לשוניות בדשבורד) |
| Executive | אבנר גולדמן | EMP-00001 | avner.goldman@example.com | מנכ"ל, דשבורד ארגוני |
| Admin + Handler + DepartmentManager | יעל הרפז | EMP-00024 | yael.harpaz@example.com | מנהלת משאבי אנוש, מנהלת המערכת ותור "כללי" |

כל מנהל/ת מחלקה מחזיק/ה בתפקיד DepartmentManager. החריג היחיד הוא המנכ"ל, שמנהל את מחלקת הנהלה ומחזיק בתפקיד Executive במקום.

**משתמשים לבדיקת מקרים מיוחדים:**

| מקרה | שם | קוד עובד | מייל |
|---|---|---|---|
| כניסה ראשונה: חייב להחליף סיסמה (`MustChangePassword`) | תומר זילבר | EMP-00009 | tomer.zilber@example.com |
| עובדת שעזבה (`IsActive = false`): לא יכולה להתחבר | אורית דנון | EMP-00012 | orit.danon@example.com |
| עובדת שהוזמנה במייל: אין לה סיסמה, וההזמנה נשלחת בהרצה הראשונה של ה-API | נגה ברק | EMP-00026 | noga.barak@example.com |

**מטפלים נוספים:**

| שם | קוד עובד | תחום |
|---|---|---|
| שי גבאי | EMP-00022 | ציוד ומחשוב |
| רותם לביא | EMP-00019 | בקשות מנהלתיות |
| מורן אשכנזי | EMP-00020 | בקשות מנהלתיות |
| עדי שמעוני | EMP-00025 | משאבי אנוש מרכזי |

## מייל הזמנה לעובד חדש

עובד חדש נוצר בלי סיסמה ומקבל מייל עם קישור חד-פעמי לקביעת סיסמה (תוקף 72 שעות). שירות רקע שולח את המיילים.

- **ברירת מחדל בפיתוח – קבצים:** כל מייל נשמר כקובץ `.eml` בתיקייה `Backend/API/App_Data/emails` (התיקייה לא נכנסת לגיט).
  פותחים את הקובץ ומעתיקים את הטוקן שאחרי `#token=` בקישור. האפשרות הזו מותרת רק ב-Development.
- **smtp4dev (שרת SMTP מקומי):** ב-`appsettings.Development.json` משנים `"Email": { "Provider": "Smtp" }`.
  שאר ההגדרות (localhost, פורט 25, בלי הצפנה) כבר קיימות. המיילים מוצגים בממשק של smtp4dev.
- **שרת אמיתי:** `Email:Smtp:Host/Port/Security/UserName` מוגדרים ב-appsettings, והסיסמה רק ב-user-secrets:
  `dotnet user-secrets set "Email:Smtp:Password" "<value>" --project API`
- `Invitations:FrontendBaseUrl` – הכתובת של אפליקציית Angular, שממנה נבנה הקישור. מחוץ ל-Development חייבת להיות https.

תרחישים מלאים – בסוף `Backend/API/API.http`.

## פקודות

| פעולה | פקודה | תיקייה |
|---|---|---|
| בנייה | `dotnet build` | `Backend/` |
| בדיקות | `dotnet test` | `Backend/` |
| Migration חדש | `dotnet ef migrations add <Name> --project Repositories --startup-project API` | `Backend/` |
| Frontend | `npm start` / `npm test` | `Frontend/` |
