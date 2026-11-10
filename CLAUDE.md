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

## כללי ארכיטקטורה (חובה)
- כל שכבה מכירה רק את השכבה שמתחתיה + Common. API לא מפנה ל-Repositories ולא ניגש ל-DbContext.
- ה-API מחזיר תמיד DTOs, לעולם לא Entities.
- מזהה המשתמש נלקח תמיד מה-JWT, לעולם לא מגוף הבקשה.
- סינון, מיון ודפדוף – תמיד בשאילתת SQL (IQueryable + Skip/Take), לא בזיכרון.
- שאילתות קריאה: AsNoTracking + Projection ל-DTO.
- כל פעולת I/O מקבלת CancellationToken.
- הרשאות נאכפות בשרת. פנייה מחוץ להיקף ההרשאה מחזירה 404 (לא 403).
- Angular: קריאות HTTP ומצב רק בשירותים; רכיבים רק מציגים. אסור innerHTML לתוכן משתמש.

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
