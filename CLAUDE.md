# עובדנט – EmployeeSupportSystem

מערכת לניהול פניות עובדים. עובד כותב פנייה בשפה חופשית, היא נשמרת מיד, ו-OpenAI מנתח אותה ברקע:
קטגוריה, דחיפות, ניתוב לגורם מטפל והצעת פתרון. המטפל מאשר/עורך ושולח תשובה לעובד.
האפיון המלא: `docs/` (עובדנט – אפיון מערכת v2.0). כשמשימה מפנה לסעיף – קרא אותו לפני התכנון.

## מצב משימות (לפי פרק 12 באפיון שב-docs/)
- הושלמו: 1.1–1.5 (תשתית), 4.1 PiiRedactor, 4.2 לקוח OpenAI (נבנו מוקדם, לפני שלב ב'),
  2.1 אימות, 2.2 מסכי התחברות, 2.3א API לניהול עובדים ומחלקות, 2.3ב מסכי ניהול עובדים ומחלקות,
  2.4 ניהול קטגוריות ו-SLA (API + מסכים), 3.1 חלק א' (API ליצירת פנייה וצפייה של עובד).
- הבא בתור: 3.1 חלק ב' – מסכי "הפניות שלי", "פנייה חדשה", "צפייה" ב-Angular.
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
  `shared/list` (ListStateDef + ListStore: מצב ב-query params, switchMap), `app-search-field` (debounce 400),
  `app-data-table` (מיון/דפדוף בשרת + מצבי 7.10), `app-filter-bar`, `applyServerErrors` (shared/forms),
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
  ל-bundle הראשוני. לא לייבא MatSnackBar/MatDialog בקוד שנטען בעלייה (core, app.config).

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
  * הודעות למסך הכניסה: `features/auth/login-notices.ts`. ב-navigation state עובר רק מפתח, והטקסט קבוע בקוד.
  * `app-new-password-fields` (shared): סיסמה חדשה + אימות, כללי המדיניות ומחוון החוזק. יושב בתוך הטופס של ההורה
    (`viewProviders` ← ControlContainer, `formControlName`). בהורה – `newPasswordControls(extraValidators)`.
    OnPush: נבדק מחדש על `control.events` ועל `ngSubmit` של ההורה, אחרת שגיאות לא מוצגות אחרי submit ריק.
  * `app-auth-brand` (features/auth): צד המותג הכחול, ו-`.auth-split-card`/`.auth-form-side` ב-`styles/_auth.scss` – משותפים
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
  * `POST /api/tickets` (JSON; קבצים ב-3.2): הפונה מ-ICurrentUser. `CreateTicketRequest` בלי שדה פונה/מחלקה, ושדות לא מוכרים
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
  * `GET /tickets/{id}`: כרגע רק לפונה (`Where(Id && RequesterId)` בשאילתה), אחרת 404. ב-3.4 היקפי הצוות נבדקים לפניו ב-`TicketService.GetAsync`.
    ציר הזמן: שינויי סטטוס בלבד, בלי `Reason` (עלול להיות פנימי – להחליט ב-3.5), `changedByName` null = מערכת.
  * DTOs של הפונה (`Common/DTOs/Tickets/RequesterTicketDtos.cs`) – אסור להוסיף Urgency, DueAt, AiStatus, ניתוח AI,
    ClassificationSource, Reason או הערות פנימיות. `RequesterJsonTests` בודק את ה-JSON ואת הטיפוסים.

## בהמשך
- משימה 2.2: לטעון את Shell ב-loadComponent (lazy), מסך ההתחברות מחוצה לו –
  כדי להוריד את ה-bundle הראשוני מתחת ל-500KB. לא להגדיל את ה-budget.

  - משימה 4.3: ה-Worker רץ בלי משתמש ← ה-Query Filter מחזיר 0 שורות. הפתרון כבר קיים (הזמנות במייל):
  לרשת מ-`PollingBackgroundService` ולעבד כל משימה ב-`CreateOrganizationScope(job.OrganizationId)`. לא IgnoreQueryFilters כללי.

  - 6.1 (בדיקות אינטגרציה): 429 ביצירת פנייה; JSON של עובד מ-GET /api/tickets/{id} בלי שדות פנימיים.
- 3.5: העובד לא רואה נימוקים בציר הזמן – רק תגובות שנכתבו אליו.

## מגבלה ידועה
  - מגבלה ידועה: אחרי השבתה או שינוי תפקידים, Access Token קיים תקף עד 15 דקות.
  פתרון אפשרי בעתיד: בדיקת IsActive בכל בקשה, עם Cache קצר.