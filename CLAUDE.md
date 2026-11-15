# עובדנט – EmployeeSupportSystem

מערכת לניהול פניות עובדים. עובד כותב פנייה בשפה חופשית, היא נשמרת מיד, ו-OpenAI מנתח אותה ברקע:
קטגוריה, דחיפות, ניתוב לגורם מטפל והצעת פתרון. המטפל מאשר/עורך ושולח תשובה לעובד.
האפיון המלא: `docs/` (עובדנט – אפיון מערכת v2.0). כשמשימה מפנה לסעיף – קרא אותו לפני התכנון.

## מצב משימות (לפי פרק 12 באפיון שב-docs/)
- הושלמו: 1.1–1.5 (תשתית), 4.1 PiiRedactor, 4.2 לקוח OpenAI (נבנו מוקדם, לפני שלב ב'),
  2.1 אימות, 2.2 מסכי התחברות, 2.3א API לניהול עובדים ומחלקות, 2.3ב מסכי ניהול עובדים ומחלקות,
  2.4 ניהול קטגוריות ו-SLA (API + מסכים), 3.1 חלק א' (API ליצירת פנייה וצפייה של עובד),
  3.1 חלק ב' (מסכי "הפניות שלי", "פנייה חדשה", "צפייה בפנייה" ב-Angular), 3.2 חלק א' (API לקבצים ותגובות),
  3.2 חלק ב' (קבצים ותגובות במסכי העובד ב-Angular).
- שינוי מבנה Frontend (לא באפיון): `src/app` עבר ממבנה core/features/shared למבנה לפי סוג קובץ – הושלם.
- הבא בתור: 3.3 – ניתוב ו-SLA.
- תוספת שלא באפיון: בדיקת איכות סיווג AI על 30 פניות לדוגמה – אחרי משימה 4.3.
- תוספת שלא באפיון: הזמנת עובד במייל (האפיון 14 שאלה 3, "בעתיד"). שלב א' (שרת) ושלב ב' (מסך set-password, סטטוס הזמנה
  ברשימה ובעריכה, שליחה מחדש, סינון לפי סטטוס הזמנה) – הושלמו.

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
  מבנה `src/app` לפי סוג קובץ:
  * `components/<אזור>/` – ‏auth, employee, handler, dashboard, admin, dev: המסכים, ‏`*.routes.ts` וקבצי עזר של האזור
    (‏`*-errors.ts`, ‏`employee-urls.ts`, ‏`login-notices.ts`, ‏`sla-rules.ts`, ‏`dev-area.ts`, ‏`testing/` של fixtures).
    `components/shared/` – רכיבים משותפים (page-header, data-table, confirm-dialog, badges, מצבי תצוגה וכו').
  * `services/` – שטוח: שירותי API, ‏auth, ‏token.store, ‏http (api-request, http-context, query-params), ‏app-areas,
    ‏notification, ‏confirm, ‏list-state/list-store, ‏server-errors, ‏hebrew-paginator-intl וכל ה-`*.store.ts` של המסכים.
    `services/testing/` – fixtures של auth.
  * `guards/` – ‏auth.guards, ‏unsaved-changes.guard. ‏`interceptors/` – ‏auth, error. ‏`models/` – הממשקים וה-enums.
    `pipes/` – כל ה-pipes. ‏`layout/` – ‏shell, header, sidebar, not-found.
  * מסכים חדשים נבנים ב-`components/<אזור>/`, שירותים ו-stores ב-`services/`, guards ב-`guards/`,
    interceptors ב-`interceptors/`, מודלים ב-`models/`, pipes ב-`pipes/`. קבצי spec ליד הקובץ שהם בודקים.
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
  * כל מסך: רכיב שנטען מנתיב (app.routes.ts או *.routes.ts ב-components).
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
- בסוף כל משימה – עדכן את סעיף 'מצב משימות'.
## החלטות שחורגות מהאפיון
- .NET 9 (האפיון: .NET 10) – לא לשדרג בלי אישור.
- FluentValidation – נכנס במשימה 2.3. Validators ב-`Services/Validators`, מופעלים מהשירות
  (`EnsureValidAsync`), לא מה-API. שגיאות ← 400 ProblemDetails עם `errors` לפי שם שדה ב-camelCase
  (גם שגיאות binding של המסגרת). הודעות בעברית ב-`WithMessage`, לא התרגום המובנה.
- "סטטוסים פתוחים" ל-SLA: New, Assigned, InProgress. WaitingForEmployee לא נכלל (השעון מושהה).
  ההגדרה במקום אחד בקוד: `Common/Constants/TicketStatuses.Open`.
- מייל וקוד עובד ייחודיים בכל המערכת (לא רק בארגון – ההתחברות לפני שידוע הארגון). מייל קיים ← 409
  "כתובת המייל כבר רשומה במערכת". קוד עובד מ-SQL SEQUENCE גלובלי (`EmployeeCodeNumbers`), ייתכנו חורים ברצף.
- כלל מנהל מחלקה (במקום אחד: `DepartmentManagerRules`): ManagerUserId ⇒ DepartmentManager או Executive;
  DepartmentManager ⇒ מנהל מחלקה. התפקיד נגזר ממסך המחלקות – מסך העובדים לא מוסיף/מסיר אותו (422).
  החלפת מנהל: החדש מקבל את התפקיד, הקודם מאבד אותו. עובד מנהל מחלקה אחת לכל היותר.
- השבתת עובד שמנהל מחלקה ← 422 (קודם להחליף מנהל). השבתת מטפל ← הפניות הלא-פתורות שלו
  (`TicketStatuses.Unresolved`, כולל WaitingForEmployee) חוזרות לתור המחלקה, הסטטוס לא משתנה.
- Admin אחרון: אי אפשר להשבית או להסיר Admin מהאחרון בארגון; נבדק תחת `sp_getapplock` בטרנזקציה.
  מנהל לא משבית את עצמו, לא מסיר מעצמו Admin ולא מאפס לעצמו סיסמה (change-password).
- מחלקה: שם ייחודי בארגון; השבתה רק בלי עובדים פעילים ובלי קטגוריה פעילה שמנותבת אליה.
- Access Token קיים נשאר תקף עד 15 דקות אחרי השבתה/שינוי תפקידים (ה-Refresh הבא כבר נכשל/מתעדכן).
- CategoryId ו-ClassificationSource מתמלאים יחד. Urgency איתם, חוץ ממסלול הגיבוי (null עד קביעה ידנית).
  כש-Urgency ריק – DueAt מחושב לפי Medium (3.4).
- להמשך (משימה 3.5): כשפנייה חוזרת מ-WaitingForEmployee – להאריך את DueAt בזמן ההמתנה.
- logout (האפיון 9.1: "מחובר") – AllowAnonymous לפי עוגיית ה-Refresh. תמיד 204 ומוחק את העוגייה,
  גם אם הטוקן כבר לא תקף או חסר.
- change-password: סיסמה נוכחית שגויה ← 400, לא נספרת לנעילת החשבון. במקום זה Rate Limit לפי משתמש
  (5 ניסיונות ל-15 דקות), וכל ניסיון כושל נרשם ב-AuditLog.
- Reuse Detection (ביטול כל הטוקנים של המשתמש) – רק לטוקן שבוטל ברוטציה (יש לו ReplacedByTokenId)
  ועברו מעל 30 שניות; תוך 30 שניות ← 401 בלבד. טוקן שבוטל ב-logout או בהחלפת סיסמה ← 401 בלבד,
  בלי ביטול כולל (אין לו "יורש" שתוקף יכול להחזיק; ביטול כולל רק ינתק את המשתמש האמיתי).


- דפוסי רשימה משותפים ב-Frontend (משימה 2.3ב) – כל מסך רשימה חדש משתמש בהם, לא בונה מחדש:
  `services/list-state.ts` + `services/list-store.ts` (ListStateDef + ListStore: מצב ב-query params, switchMap), `app-search-field` (debounce 400),
  `app-data-table` (מיון/דפדוף בשרת + מצבי 7.10), `app-filter-bar`, `applyServerErrors` (services/server-errors.ts),
  `ConfirmService`, `app-form-alert`, `dateTime` pipe (dd/MM/yyyy HH:mm, Asia/Jerusalem).
- רשימת עובדים: מיון ברירת מחדל קוד עובד יורד (החדשים בראש) – בשרת `UserSortFields.Default` ובלקוח
  `USERS_LIST.defaultSort`; לשנות בשניהם יחד. בלי sortDir: עמודת ברירת המחדל יורדת, כל עמודה אחרת עולה.
- 422 `manages-other-department` (עובד כבר מנהל מחלקה אחרת) נשאר 422 בשרת; ה-Frontend מציג אותו ליד שדה המנהל
  (`typeFields` של applyServerErrors).
- Cache של נתוני ייחוס (משימה 2.4, 11.3): `IReferenceDataCache` (מימוש `MemoryReferenceDataCache`), מפתח אחד לכל ארגון
  וסוג (`departments:{org}`, `categories:{org}`) עם הרשימה המלאה, 10 דקות. הסינון לפעילות בלבד נעשה על הרשימה שב-Cache.
  הארגון מועבר במפורש (ה-Worker של 4.3 בלי משתמש). שינוי מחלקה או קטגוריה מנקה את המפתח, וגם שינוי עובד ב-UserService
  מנקה את המחלקות (ב-DTO יש מונים ומנהל).
- קטגוריות: קוד `^[A-Z][A-Z_]{1,29}$`, ייחודי בארגון, לא קיים ב-UpdateCategoryRequest. IsFallback לא נמצא באף בקשה;
  אינדקס ייחודי מסונן `IX_Categories_OrganizationId_Fallback` (לכל היותר גיבוי אחד). השבתת הגיבוי ← 422
  `fallback-category-deactivation`. FixedDepartment ⇒ מחלקה פעילה (400 על handlingDepartmentId), נבדק בכל שמירה של
  קטגוריה פעילה. RequesterDepartmentHr ⇒ handlingDepartmentId null (400). POST/PUT מחזירים
  `CategorySaveResult { category, warnings }`; אזהרה כשאין במחלקה מטפלים פעילים.
- SLA: GET לכל משתמש מחובר (האפיון 9.3: מנהל מערכת). PUT בצורה שטוחה `{ critical, high, medium, low }`,
  שעות שלמות 1–8760, Critical ≤ High ≤ Medium ≤ Low (השגיאה על הרמה הפחות דחופה). לא מעדכן DueAt קיים.
  ברירות המחדל ב-`Common/Constants/SlaDefaults` (גם ל-Seed וגם לרמה חסרה).
- `FormAlert.tone: 'warning'` + `dismissible` – לאזהרה שלא חסמה פעולה ונשארת במסך.
- `NotificationService` טוען את MatSnackBar ב-dynamic import (נטען מיד בעלייה), כדי שה-CDK overlay לא ייכנס
  ל-bundle הראשוני. לא לייבא MatSnackBar/MatDialog בקוד שנטען בעלייה (app.config, interceptors, guards ושירותי ה-auth/http שהם טוענים).

- הזמנת עובד במייל (תוספת, במקום סיסמה זמנית ביצירת עובד; האפיון 4.2 מדבר על סיסמה זמנית):
  * POST /users יוצר עובד עם `PasswordHash = null` ו-`MustChangePassword = true`, ומחזיר `UserDetailsDto` (בלי סיסמה זמנית).
    באותה טרנזקציה נוספת שורה ל-`InvitationEmailJobs`: רק "הזמנה לעובד X" – בלי טוקן, בלי מייל ובלי תוכן.
  * `MustChangePassword` = "העובד עוד לא בחר סיסמה משלו". שליחה מחדש (`POST /users/{id}/invitation`) מותרת רק כשהוא true
    (אחרת 422 `password-already-set`; עובד לא פעיל ← 422 `inactive-user`). reset-password עדיין נותן סיסמה זמנית ומבטל הזמנות פתוחות.
  * login עם `PasswordHash` null ← אימות מול ה-Dummy Hash ואותו 401 אחיד, נספר לנעילה. AuditLog `LoginFailed.PasswordNotSet`.
  * הטוקן נוצר רק ברגע השליחה (`InvitationEmailProcessor`), ו-`UserInvitations` שומר רק SHA-256 (`SecureTokenGenerator`,
    לשעבר RefreshTokenGenerator). כל ניסיון שליחה יוצר טוקן חדש ומבטל את הקודם – לכל עובד לכל היותר קישור תקף אחד.
    תוקף 72 שעות מרגע השליחה. הקישור: `{FrontendBaseUrl}/set-password#token=...` (אחרי #).
  * תור כמו AiAnalysisJobs: claim ב-`UPDATE ... OUTPUT` עם READPAST, ‏Attempts עולה בכל claim, נעילה של 5 דקות.
    ניסיונות חוזרים אחרי 1/5/30 דקות, ‏MaxAttempts=4. כישלון קבוע (נמען נדחה 5xx) ← Failed מיד. שגיאות SMTP אחרות, כולל אימות,
    נחשבות זמניות. Attempts > Max (קריסות חוזרות) ← Failed. `Status` הוא Concurrency Token: ביטול באמצע ← ה-Worker לא שולח.
  * set-password (אנונימי, ‏Rate Limit של 10 ל-15 דקות לכל IP): קישור לא קיים / נוצל / בוטל / פג / עובד לא פעיל ← 400
    `invalid-invitation` עם "הקישור אינו בתוקף. יש לפנות למנהל המערכת." (כולל טוקן חסר או ארוך מדי, על השדה token).
    סימון הנוצל ב-UPDATE מותנה (`TryMarkUsedAsync`). בהצלחה 204 בלי התחברות אוטומטית, ביטול שאר ההזמנות, המשימות וה-Refresh Tokens.
  * סטטוס ב-`UserListItemDto`/`UserDetailsDto` (`invitation: { status, at }` או null): לפי המשימה האחרונה שאינה Cancelled.
    Pending/Sent/Used/Expired/Failed + ‏Revoked (קישור שבוטל באיפוס סיסמה). משימה שבוטלה לפני שליחה ← הסטטוס מהקודמת, או null.
  * מייל: `IEmailSender` – ‏`SmtpEmailSender` (MailKit 4.18.2; ‏4.8.0 שתוכננה מסומנת בפגיעות NU1902) או `FileEmailSender`
    (קבצי .eml ב-`App_Data/emails`, מותר רק ב-Development, אחרת האפליקציה לא עולה). ‏`Email:Smtp:Password` רק ב-user-secrets.
    `Invitations:FrontendBaseUrl` חייב https מחוץ ל-Development. בלוג רק קוד שגיאה וסוג חריגה – לא טוקן, לא מייל, לא הודעת SMTP.
  * AuditLog: `Invitation.Sent` / `Invitation.SendFailed` (מערכת: UserId null, ‏IpAddress "system"), `Invitation.Resent`,
    `Invitation.PasswordSet`, `Invitation.PasswordSetFailed` (בלי actor).
  * תבנית המייל: `Services/Implementations/Invitations/Templates/invitation.html` (לא `*.he.html` – MSBuild מתייחס ל-he כתרבות).
  * סינון `GET /users?invitationStatus=` (נוסף בשלב ב'): אותו CASE כמו העמודה – `InvitationStatusOf`/`InvitationStatusAt`
    ב-UserRepository הם ההגדרה היחידה, וה-Projection וה-Filter נבנים מהם. עובד שלא הוזמן לא מתאים לאף סטטוס.
- הזמנה במייל – צד לקוח (שלב ב'):
  * `/set-password` מחוץ ל-shell, בלי guards. הטוקן נקרא מה-fragment (`ActivatedRoute.snapshot.fragment`) ונמחק מהכתובת מיד
    וגם ב-NavigationEnd הראשון (ה-Router כותב את ה-URL בסוף הניווט הראשון). נשמר בשדה פרטי (לא signal, לא DOM) עד תשובה סופית:
    הצלחה או `invalid-invitation`. בשגיאה זמנית (רשת/429/500) נשאר, כדי לנסות שוב. משתמש מחובר ← `AuthService.signOut()`
    (ביטול בשרת וניקוי מקומי בלי ניווט; `logout()` משתמש בו). `<meta name="referrer" content="no-referrer">` רק בזמן שהמסך פתוח.
  * set-password נחשב בקשת session (`SESSION_ENDPOINTS` ב-api-request.ts): בלי Access Token ובלי refresh על 401.
  * הודעות למסך הכניסה: `components/auth/login-notices.ts`. ב-navigation state עובר רק מפתח, והטקסט קבוע בקוד.
  * `app-new-password-fields` (components/shared): סיסמה חדשה + אימות, כללי המדיניות ומחוון החוזק. יושב בתוך הטופס של ההורה
    (`viewProviders` ← ControlContainer, `formControlName`). בהורה – `newPasswordControls(extraValidators)`.
    OnPush: נבדק מחדש על `control.events` ועל `ngSubmit` של ההורה, אחרת שגיאות לא מוצגות אחרי submit ריק.
  * `app-auth-brand` (components/auth): צד המותג הכחול, ו-`.auth-split-card`/`.auth-form-side` ב-`styles/_auth.scss` – משותפים
    ל-login ול-set-password.
  * `app-invitation-badge`: טקסט וצבע לכל סטטוס (ממתינה accent, נשלחה primary, נוצלה success, פגה warning, נכשלה danger,
    בוטלה neutral; בלי הזמנה "—"). "שליחת הזמנה מחדש" מוצג לעובד פעיל עם `mustChangePassword` שההזמנה שלו לא נוצלה,
    ולא למשתמש המחובר. בסטטוס נכשלה – אזהרה עם שליחה מחדש או סיסמה זמנית. ה-confirm של איפוס מזכיר שההזמנה תבוטל.
    אחרי איפוס – טעינה מחדש של העובד, בלי לאפס את הטופס.
- הקשר ארגון לשירותי רקע (לשימוש גם ב-4.3): `AppDbContext` תלוי ב-`IOrganizationContext` (לא ב-ICurrentUser).
  `OrganizationContext` (Scoped): המשתמש המחובר, אחרת הארגון שנקבע ב-`ISystemOrganizationContext.Enter` (זורק אם יש משתמש
  או ארגון אחר). ‏`API/BackgroundJobs`: ‏`PollingBackgroundService` (בסיס ל-Worker) ו-`CreateOrganizationScope(orgId)`.
  `InvitationEmailJobs` היא הישות היחידה עם OrganizationId בלי Query Filter (ה-claim חוצה ארגונים ומחזיר רק מזהים).
  טבלת תור של 4.3 תעבוד באותו דפוס: OrganizationId בשורה, claim בלי סינון, עיבוד בתוך Scope של הארגון.

- פניות – צד העובד (משימה 3.1א):
  * `POST /api/tickets` (מ-3.2: multipart עם קבצים): הפונה מ-ICurrentUser. `CreateTicketRequest` בלי שדה פונה/מחלקה, ושדות לא מוכרים
    בגוף נזרקים. המחלקה נקראת מה-DB (`GetActiveDepartmentIdAsync`), לא מה-claim. עובד לא פעיל ← 401 (גם בחלון 15 הדקות).
    Status=New, ‏AiStatus=Pending, בלי קטגוריה/דחיפות/שיוך/DueAt. נשמרת גם שורת היסטוריה `null → New` (ChangedBy = הפונה).
    Trim לנושא ולתיאור, נושא ריק ← null. ‏201 + Location + `{ id }`. מגבלות ב-`Common/Constants/TicketFieldLimits`.
  * כל מה שנשמר עם פנייה חדשה – ב-`TicketService.StageNewTicket` (navigations, ‏SaveChanges אחד = טרנזקציה אחת).
    שם יתווספו ניתוב ו-SLA (3.3) ומשימת AI (4.3). save שני ← לעטוף ב-`ExecuteInTransactionAsync`.
  * Rate Limit `create-ticket`: ‏20 לשעה לכל משתמש (Fixed Window, גם בקשות 400 נספרות). ב-429 יש `detail` לפי ה-policy
    (`RejectionDetails` ב-AuthExtensions).
  * `GET /tickets/my`: ‏`status` (כמה ערכים), ‏`search` (LIKE על Subject בלבד), מיון createdAt (ברירת מחדל יורד) / updatedAt / status
    (סדר ה-enum), שובר שוויון Id יורד. בלי נושא ← 80 התווים הראשונים של התיאור + `isSubjectExcerpt` (ה-"…" בלקוח).
  * `GET /tickets/my/summary`: שאילתה מקובצת אחת. פתוחות = `TicketStatuses.RequesterOpen` (New, Assigned, InProgress, Resolved),
    ממתינות לתגובתי = WaitingForEmployee, נסגרו החודש = Closed עם ClosedAt מתחילת החודש בשעון ישראל (`IsraelTime`). Cancelled – באף כרטיס.
  * `GET /tickets/{id}`: מ-3.2 דרך `TicketAccess` (כל ההיקפים), אחרת 404. ב-3.4 – DTO נפרד לצוות.
    ציר הזמן: שינויי סטטוס בלבד, בלי `Reason` (עלול להיות פנימי – להחליט ב-3.5), `changedByName` null = מערכת.
  * DTOs של הפונה (`Common/DTOs/Tickets/RequesterTicketDtos.cs`) – אסור להוסיף Urgency, DueAt, AiStatus, ניתוח AI,
    ClassificationSource, Reason או הערות פנימיות. `RequesterJsonTests` בודק את ה-JSON ואת הטיפוסים.

- פניות – צד העובד ב-Frontend (משימה 3.1ב):
  * נתיבים: `/employee` (הפניות שלי), `/employee/tickets/new` (עם `unsavedChangesGuard`), `/employee/tickets/:id`.
    ה-URL-ים ב-`components/employee/employee-urls.ts`.
  * קבוצות סטטוס (כרטיסי מדד + כפתורי פלח) – `REQUESTER_STATUS_GROUPS` ב-`models/ticket.models.ts`, מקום אחד:
    open = ‏New/Assigned/InProgress/Resolved (כמו `TicketStatuses.RequesterOpen`), waiting = WaitingForEmployee,
    closed = Closed + Cancelled. ב-URL ‏`?view=open|waiting|closed`, לשרת נשלח `status` חוזר.
    כרטיס "נסגרו החודש" מסנן ל-closed ומציג את כל הסגורות והמבוטלות (אין סינון תאריך בשרת).
  * `toHttpParams` שולח מערך כפרמטר חוזר (`?status=New&status=Assigned`).
  * `app-data-table`: ‏`rowClass` (מעוצב שם: `row-attention`) ו-`<ng-template appRowCard>` – ברוחב XSmall כרטיסים במקום טבלה.
    לשימוש גם בתור המטפל (3.4).
  * `unsavedChangesGuard` + `HasUnsavedChanges` (guards/unsaved-changes.guard.ts): confirm ביציאה עם טקסט. במסך פנייה חדשה גם `beforeunload`.
    אחרי שליחה מוצלחת – בלי confirm, ומעבר לצפייה עם `replaceUrl`.
  * `app-status-badge` (צבעים `--status-*` ב-styles.scss), `statusLabel` pipe, `app-relative-time` (טקסט יחסי + tooltip ותאריך
    מלא לקורא מסך). `formatRelativeTime` כתוב ידנית בעברית (צורת זוגי) – `Intl.RelativeTimeFormat('he')` נותן "לפני שעתיים (2)".
  * מונה התווים מוכרז ב-`aria-live` רק 800ms אחרי הפסקת הקלדה.

- קבצים ותגובות – API (משימה 3.2א):
  * `TicketAccess` (`Services/Implementations/Tickets`): כל endpoint של פנייה בודדת מתחיל בו. הצופה (מחלקה, מחלקה מנוהלת
    לפי `Departments.ManagerUserId`, תפקידים) ועמודות הניתוב של הפנייה נקראים מה-DB, לא מה-token. משתמש לא פעיל ← 401.
    המטריצה עצמה ב-`TicketAccessPolicy` (טהורה):
    - היקפים: פונה; מטפל (משויך אליו, או `AssignedDepartmentId` = המחלקה שלו); מנהל מחלקה (מחלקת הפונה או המחלקה המטפלת
      = המחלקה שהוא מנהל); מנכ"ל (כל הארגון). מחוץ להיקף ← 404.
    - פונה שהוא גם איש צוות ← **תצוגת פונה** (בלי פנימי). `CanSeeInternal`: מטפל, מנהל ומנכ"ל, לא הפונה.
    - `CanReply` (תגובה גלויה וקובץ בלי תגובה): הפונה או מטפל. `CanWriteInternal`: מטפל או מנהל, לא הפונה. מנכ"ל רק צופה.
  * `ForbiddenException` ← 403 (`internal-comment-not-allowed`, `reply-not-allowed`). סדר הבדיקות: 400 ← 404 ← 403 ← 422
    (`ticket-closed` ל-Closed/Cancelled).
  * POST /tickets, ‏/comments, ‏/attachments – **multipart בלבד** (JSON ← 415). מגבלת בקשה לכל endpoint:
    `AttachmentLimits.MaxRequestBytes` (16MB). חריגה ← 413 בעברית: ב-binding לפי Content-Length מול המגבלה, ובמידלוור
    `BadHttpRequestException` 413.
  * קבצים: עד 3 בבקשה, עד 5MB, ‏pdf/jpg(jpeg)/png/docx. הסוג נקבע **מהסיומת של השם אחרי ניקוי** וגם מהתוכן
    (`AttachmentFileTypes`; ‏docx = ZIP עם `word/document.xml`, בלי `vbaProject.bin`). ‏ContentType נשמר לפי הסוג שאומת.
    `FileNameSanitizer`: רק הקטע האחרון, NFC, בלי Cc/Cf (כולל U+202E), בלי `<>:"|?*`, עד 120 תווים כולל הסיומת.
  * אחסון: `IFileStorage` ב-Common (ה-Seeder משתמש בו), ‏`LocalFileStorage` ב-`Attachments:Directory`
    (`App_Data/attachments`, לא תחת wwwroot – נבדק בעלייה). שם בדיסק: GUID בלי סיומת, ונבדק מול `^[0-9a-f]{32}$` לפני כל גישה.
    `StagedFiles`: ולידציה ← כתיבה ← SaveChanges ← Commit. בלי Commit (כשל במסד, ביטול) הקבצים נמחקים.
  * קובץ של הערה פנימית הוא פנימי בעצמו (נגיש רק דרך התגובה). קבצי הפנייה החדשה ו-/attachments: ‏`CommentId` null, גלויים.
  * תגובה גלויה וקובץ מעדכנים `Ticket.UpdatedAt` (Tracked ← RowVersion משתנה, ושינוי מתחרה ← 409). **הערה פנימית לא
    מעדכנת** – אחרת העובד היה רואה "עודכן" בלי שום דבר גלוי.
  * GET /history: ‏`RequesterTicketHistoryDto` או `StaffTicketHistoryDto` (‏`ITicketHistoryDto`). פריטים פולימורפיים עם
    `kind`: ‏status / comment / attachment. שלוש שאילתות מהפנייה המסוננת (סינון פנימי ונימוקים בשאילתה), ומיזוג בזיכרון לפי
    (זמן, סוג, Id). ‏`RequesterTicketDetailsDto.Timeline` מ-3.1 נשאר.
  * הורדה: 404 זהה לקובץ פנימי ולקובץ שלא קיים. `nosniff`, ‏`CSP: sandbox`, ‏`no-store`, ‏`Content-Disposition: attachment`
    עם `filename*` (חשוף ב-CORS). AuditLog `Attachment.Downloaded` (EntityType `TicketAttachment`) – רק אחרי שהקובץ נפתח.
  * GET /tickets/{id} עובר דרך TicketAccess. גם איש צוות מקבל כרגע את ה-DTO של הפונה (תצוגת צוות – 3.4).
  * Seeder: מרוקן את תיקיית הקבצים (רק שמות GUID) ומצרף PNG/PDF לכ-10% מהפניות (`SampleAttachments`, בלי ה-Random של
    הפניות). דוגמאות ל-API.http ב-`API/HttpSamples`. פנייה 149 של נועה מטופלת ע"י עדי שמעוני, 49 סגורה.

- קבצים ותגובות – צד העובד ב-Frontend (משימה 3.2ב):
  * מגבלות בלקוח – `models/attachment.models.ts` (`ATTACHMENT_LIMITS`, סיומות, `accept`) ו-`COMMENT_FIELD_LIMITS`, אותם ערכים
    כמו בשרת. `validateFiles` (services/attachment-validation.ts): ריק ← גודל ← סיומת ← כפילות ← מעל 3, בנוסח הודעות השרת.
    בדיקת התוכן רק בשרת.
  * `app-file-upload` (components/shared): ControlValueAccessor עם ערך `File[]`, כפתור בחירה (מקלדת) + גרירה, דחיות
    באזור `aria-live` קבוע. `serverError` לשגיאת שרת, `busy` נועל בזמן שליחה **בלי** disable לטופס (enable מריץ את
    ה-validators מחדש ומוחק שגיאות שרת). שגיאות שרת על קבצים: `attachmentServerError` (400 `files`/`files[i]` עם שם הקובץ, 413),
    ו-`applyServerErrors(..., { skipFields: ['files'] })` כדי שלא יוצגו פעמיים.
  * `app-timeline` (components/shared) – הציר היחיד של פנייה (status / comment / attachment מ-GET /history), החליף את
    `ticket-timeline` של 3.1. מציג מה שהשרת החזיר, בלי סינון. מיועד גם לתצוגת הצוות ב-3.4 (יקבל אז גם הערות פנימיות).
  * `app-attachment-chip` + `AttachmentDownloadService`: הורדה ב-HttpClient כ-blob (Bearer), שם מ-`filename*` ואז `filename`
    (`fileNameFromContentDisposition`), Object URL משוחרר מיד אחרי ה-click. 404 ← "הקובץ לא נמצא או שאין לך גישה אליו" (toast).
  * תגובה: טקסט (עם/בלי קבצים) ← POST /comments; קבצים בלי טקסט ← POST /attachments; בלי שניהם ← שגיאה מקומית, לא נשלח.
    `TicketViewStore.sendReply` מסתיים רק אחרי טעינה מחדש של הפנייה וה-history (התגובה כבר בציר כשהשדות מתאפסים).
    Closed/Cancelled או 422 `ticket-closed` ← "הפנייה סגורה ולא ניתן להוסיף תגובות" במקום אזור התגובה. 409 ← הודעה + טעינה מחדש.
  * `HasUnsavedChanges.unsavedChangesMessage?()` + `unsavedMessage(hasText, hasFiles)`: נוסח ה-confirm לטקסט / קבצים / שניהם.
    הנתיב `tickets/:id` מוגן ב-`unsavedChangesGuard` + `beforeunload`.
  * מונה תווים משותף: `components/employee/character-count.ts` (`describeCount`, `COUNTER_ANNOUNCE_DELAY_MS`).

## בהמשך
- משימה 2.2: לטעון את Shell ב-loadComponent (lazy), מסך ההתחברות מחוצה לו –
  כדי להוריד את ה-bundle הראשוני מתחת ל-500KB. לא להגדיל את ה-budget.

  - משימה 4.3: ה-Worker רץ בלי משתמש ← ה-Query Filter מחזיר 0 שורות. הפתרון כבר קיים (הזמנות במייל):
  לרשת מ-`PollingBackgroundService` ולעבד כל משימה ב-`CreateOrganizationScope(job.OrganizationId)`. לא IgnoreQueryFilters כללי.

  - 6.1 (בדיקות אינטגרציה): 429 ביצירת פנייה; JSON של עובד מ-GET /api/tickets/{id} בלי שדות פנימיים.
- 3.5: העובד לא רואה נימוקים בציר הזמן – רק תגובות שנכתבו אליו.
- 3.5: נקודת החיבור ב-`TicketConversationService.AddCommentAsync`: תגובת פונה ב-WaitingForEmployee ← InProgress
  (TicketStatusRules + שורת היסטוריה, באותו save). ‏IsResolution נקבע רק משם ("שלח פתרון").
- 3.4: אותם היקפים של `TicketAccessPolicy` כ-Expression לרשימות הצוות (עם בדיקה ששתי הגרסאות תואמות). תצוגת צוות
  ל-GET /tickets/{id}. ‏AuditLog "צפייה בפנייה של עובד אחר" (6.4).
- 4.3: ה-AI מקבל רק את התיאור המנוקה – לא קבצים ולא שמות קבצים (הערה ב-`IAiClient` וב-`StageNewTicket`).
- פתוח (מחוץ ל-3.2): ניקוי קבצים יתומים (כשל במחיקה אחרי כשל במסד – נרשם Warning), סריקת וירוסים, Rate Limit לתגובות.

## מגבלה ידועה
  - מגבלה ידועה: אחרי השבתה או שינוי תפקידים, Access Token קיים תקף עד 15 דקות.
  פתרון אפשרי בעתיד: בדיקת IsActive בכל בקשה, עם Cache קצר.