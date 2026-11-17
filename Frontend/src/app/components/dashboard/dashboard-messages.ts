/**
 * The texts of the dashboards. The card explanations follow the definitions of the metrics in the server – only in
 * Repositories/Implementations/DashboardMetrics.cs (CLAUDE.md "דשבורדים – API") – change them together.
 */
export const CARD_HINTS = {
  open: 'נכון לעכשיו, בלי קשר לטווח: פניות חדשות, משויכות, בטיפול וממתינות לעובד (פנייה שנפתרה אינה פתוחה).',
  newThisWeek: 'פניות שנפתחו ב-7 הימים האחרונים, כולל היום (שעון ישראל). בלי קשר לטווח.',
  overdue:
    'נכון לעכשיו: פניות פתוחות שעבר מועד היעד שלהן. פנייה שממתינה לעובד לא נספרת – השעון שלה מושהה.',
  averageResolution:
    'מהפניות שנפתרו בטווח: הזמן הממוצע מפתיחת הפנייה ועד הפתרון האחרון שלה, בשעות קלנדריות וכולל זמן ההמתנה לעובד.',
  slaCompliance:
    'מהפניות שנפתרו בטווח: האחוז שנפתרו עד מועד היעד. מועד היעד כבר כולל את זמן ההמתנה לעובד ואת הפתיחה מחדש.',
  autoClassified: 'מהפניות שנפתחו בטווח: האחוז שהסיווג שלהן נקבע ע"י ה-AI (לא ידני ולא קטגוריית הגיבוי).',
} as const;

/** Under the charts' headings: what they count. */
export const CHART_NOTES = {
  opened: 'פניות שנפתחו בטווח',
  status: 'פניות שנפתחו בטווח, לפי הסטטוס הנוכחי שלהן',
  trend: 'נסגרו = נסגרו סופית באותו יום (ביטול לא נספר)',
} as const;

export const NO_RESOLVED_IN_RANGE = 'אין פניות שנפתרו בטווח';
export const NO_OPENED_IN_RANGE = 'אין פניות שנפתחו בטווח';
export const OVERDUE_ATTENTION = 'דורש טיפול';

export const EMPTY_RANGE_HEADING = 'אין פניות בטווח שנבחר';
export const EMPTY_RANGE_DESCRIPTION = 'אפשר לבחור טווח תאריכים אחר.';

export const NO_MANAGED_DEPARTMENT_HEADING = 'לא נמצאה מחלקה בניהולך';
export const NO_MANAGED_DEPARTMENT_DESCRIPTION = 'הדשבורד המחלקתי מוצג למנהל/ת של מחלקה. יש לפנות למנהל המערכת.';

export const DEPARTMENT_NOT_FOUND_HEADING = 'המחלקה לא נמצאה';
export const DEPARTMENT_NOT_FOUND_DESCRIPTION = 'ייתכן שהקישור שגוי, או שאין לך הרשאה לצפות בדשבורד של מחלקה זו.';

export const REFRESHED_ANNOUNCEMENT = 'הנתונים עודכנו';

export const UNCLASSIFIED_CATEGORY = 'טרם סווגה';

export const VIEW_LABELS = {
  Requesters: 'פניות של עובדי המחלקה',
  Handling: 'פניות בטיפול המחלקה',
} as const;
