# עובדנט – EmployeeSupportSystem

מערכת לניהול פניות עובדים. עובד כותב פנייה בשפה חופשית, היא נשמרת מיד, ו-OpenAI מנתח אותה ברקע:
קטגוריה, דחיפות, ניתוב לגורם מטפל והצעת פתרון. המטפל מאשר/עורך ושולח תשובה לעובד.
האפיון המלא: `docs/` (עובדנט – אפיון מערכת v2.0). כשמשימה מפנה לסעיף – קרא אותו לפני התכנון.

## שפה
- הסברים, תוכניות ושאלות אליי – בעברית.
- קוד, שמות משתנים/מחלקות, הערות בקוד והודעות commit – באנגלית.
- טקסט שמוצג למשתמש בממשק ובהודעות שגיאה (ProblemDetails) – בעברית.

## מבנה
- `Backend/` – ASP.NET Core Web API, .NET 9 (net9.0), Solution: `EmployeeSupportSystem.sln`
  - `API` – Controllers, Middleware, BackgroundJobs, Program.cs. HTTP בלבד, בלי לוגיקה עסקית.
  - `Services` – לוגיקה עסקית, AI, ניתוב, SLA, ולידציה (FluentValidation), Mapping ידני.
  - `Repositories` – גישה לנתונים: AppDbContext, Fluent API Configurations, Migrations, Seed.
  - `Common` – Entities, DTOs, Enums, Interfaces, Options, Exceptions. בלי לוגיקה ובלי תלויות.
- `Frontend/` – Angular 20, Standalone Components + Signals, Angular Material, RTL מלא.
   - עיצוב: כל מסך ורכיב ב-Frontend לפי docs/design-guide.md. קבצי docs/design-reference/ הם השראה לסגנון בלבד – לא להעתיק מהם תוכן או לוגיקה.

## כללי ארכיטקטורה (חובה)
- כל שכבה מכירה רק את השכבה שמתחתיה + Common. API לא מפנה ל-Repositories ולא ניגש ל-DbContext.
- ה-API מחזיר תמיד DTOs, לעולם לא Entities.
- מזהה המשתמש נלקח תמיד מה-JWT, לעולם לא מגוף הבקשה.
- סינון, מיון ודפדוף – תמיד בשאילתת SQL (IQueryable + Skip/Take), לא בזיכרון.
- שאילתות קריאה: AsNoTracking + Projection ל-DTO.
- כל פעולת I/O מקבלת CancellationToken.
- הרשאות נאכפות בשרת. פנייה מחוץ להיקף ההרשאה מחזירה 404 (לא 403).
- Angular: קריאות HTTP ומצב רק בשירותים; רכיבים רק מציגים. אסור innerHTML לתוכן משתמש.
- רכיבי Angular – כל רכיב בתיקייה משלו.
  שלושה קבצים נפרדים (name.ts, name.html, name.scss עם templateUrl + styleUrl) – חובה עבור:
  * כל מסך: רכיב שנטען מנתיב (app.routes.ts או *.routes.ts ב-features).
  * רכיבי layout: shell, header, sidebar.
  * כל רכיב שהתבנית שלו מעל ~20 שורות או שיש לו עיצוב משלו של יותר מכמה שורות.
  רכיב קטן ופשוט (תג, כפתור, מצב ריק) – מותר inline. כשהוא גדל מעבר לזה – להעביר לקבצים.
- Services לא משתמש בטיפוסי HTTP (HttpContext, IActionResult, Request וכו'), למרות ה-FrameworkReference.
  מידע על המשתמש – רק דרך ICurrentUser.

## פרטיות ו-AI (חובה)
- ל-OpenAI נשלח רק טקסט הפנייה אחרי PiiRedactor. לעולם לא: שם, קוד עובד, מייל, מחלקה, קבצים.
- טקסט פניות, תגובות, סיסמאות, טוקנים ותשובות AI – לא נכתבים ללוגים.
- אם ה-AI נכשל או לא זמין – הפנייה עוברת לקטגוריה GENERAL. אף פנייה לא נתקעת.

## סודות
- מפתח OpenAI, מפתח JWT ומחרוזת חיבור – רק ב-`dotnet user-secrets`. לעולם לא ב-appsettings.json ולא בגיט.
- `appsettings.Example.json` מכיל את המבנה עם ערכים ריקים.

## פקודות
- בנייה: `dotnet build` מתוך `Backend/`
- בדיקות: `dotnet test` מתוך `Backend/`
- Migration: `dotnet ef migrations add <Name> --project Repositories --startup-project API`
- Frontend: `npm start` / `npm test` מתוך `Frontend/`

## דרך עבודה
- לפני שינוי מבני – הצג תוכנית וחכה לאישור.
- אחרי שינוי בקוד C# – הרץ `dotnet build` וודא 0 שגיאות.
- אל תבצע commit או push – אני עושה זאת.
- אל תתקין חבילות NuGet/npm בלי לציין קודם מה ולמה (חיבור אינטרנט מוגבל).
- אם משהו באפיון לא ברור או סותר – שאל, אל תנחש.
## החלטות שחורגות מהאפיון
- .NET 9 (האפיון: .NET 10) – לא לשדרג בלי אישור.
- FluentValidation – נדחה לשלב מאוחר.
- "סטטוסים פתוחים" ל-SLA: New, Assigned, InProgress. WaitingForEmployee לא נכלל (השעון מושהה).
  ההגדרה במקום אחד בקוד.
- CategoryId ו-ClassificationSource מתמלאים יחד. Urgency איתם, חוץ ממסלול הגיבוי (null עד קביעה ידנית).
  כש-Urgency ריק – DueAt מחושב לפי Medium (3.4).
- להמשך (משימה 4.2): כשפנייה חוזרת מ-WaitingForEmployee – להאריך את DueAt בזמן ההמתנה.


## בהמשך
- משימה 2.2: לטעון את Shell ב-loadComponent (lazy), מסך ההתחברות מחוצה לו –
  כדי להוריד את ה-bundle הראשוני מתחת ל-500KB. לא להגדיל את ה-budget.