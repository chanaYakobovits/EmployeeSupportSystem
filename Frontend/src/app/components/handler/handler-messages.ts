import type { BulkAction, BulkFailure } from '../../models/ticket.models';

/** Spec 4.4 / 11.2: the wording of a concurrency conflict (409), shown after the data was reloaded. */
export const CONFLICT_MESSAGE = 'הפנייה עודכנה על ידי משתמש אחר. הנתונים רועננו – בדקי ונסי שוב.';

/** After a change that moved the ticket out of the user's scopes (TicketUpdated.canView false). */
export const LEFT_SCOPE_MESSAGE = 'הפנייה הועברה ואינה עוד בטיפולך. חזרת לתור הפניות.';

export const TAKEN_MESSAGE = 'הפנייה שויכה אלייך';
export const TAKE_FAILED_MESSAGE = 'לא ניתן לקחת את הפנייה לטיפול. הרשימה רועננה.';

// ===================== Bulk update (task 3.6) =====================

/** The single action's error types that say more than the reason (TicketStatusService / TicketHandlingService). */
const BULK_ERROR_TEXT: Readonly<Record<string, string>> = {
  'ticket-assigned-to-other': 'משויכת למטפל אחר',
  'ticket-not-assigned': 'יש לקחת את הפנייה לטיפולך קודם',
  'ticket-not-open': 'לא ניתן להעביר פנייה שנפתרה, נסגרה או בוטלה',
  'assignee-is-requester': 'המטפל שנבחר הוא העובד שפתח את הפנייה',
  'assignment-unchanged': 'הפנייה כבר משויכת ליעד שנבחר',
  'assignee-not-handler': 'המטפל שנבחר אינו מטפל במחלקה המטפלת בפנייה',
  'ticket-not-routed': 'הפנייה עדיין לא נותבה למחלקה מטפלת',
  'department-has-no-handlers': 'במחלקה שנבחרה אין מטפלים פעילים',
};

/** Why one ticket of a bulk action was not updated, in Hebrew (spec 11.2 reasons, refined by the error type). */
export function bulkFailureText(failure: BulkFailure, action: BulkAction): string {
  switch (failure.reason) {
    case 'Conflict':
      return 'עודכנה על ידי משתמש אחר';
    case 'NotFound':
      return 'לא נמצאה או שאין לך גישה אליה';
    case 'InvalidTransition':
      return (
        errorText(failure) ??
        (action === 'start' ? 'לא ניתן להתחיל טיפול בסטטוס הנוכחי' : 'לא ניתן להעביר את הפנייה בסטטוס הנוכחי')
      );
    case 'InvalidTarget':
      return errorText(failure) ?? 'היעד שנבחר אינו מתאים לפנייה';
  }
}

function errorText(failure: BulkFailure): string | undefined {
  return failure.errorType ? BULK_ERROR_TEXT[failure.errorType] : undefined;
}

/** "פנייה אחת" / "3 פניות". */
export function ticketCount(count: number): string {
  return count === 1 ? 'פנייה אחת' : `${count} פניות`;
}

/** The headline of the result: how many were updated (and how many were not). */
export function bulkResultSummary(succeeded: number, failed: number): string {
  const done =
    succeeded === 0 ? 'אף פנייה לא עודכנה' : succeeded === 1 ? 'פנייה אחת עודכנה בהצלחה' : `${succeeded} פניות עודכנו בהצלחה`;
  if (failed === 0 || succeeded === 0) {
    return done;
  }
  return `${done}, ${failed === 1 ? 'פנייה אחת לא עודכנה' : `${failed} פניות לא עודכנו`}`;
}

/** Shown when the bulk request itself failed (network, 500): the list was reloaded. */
export const BULK_FAILED_MESSAGE = 'הפעולה לא בוצעה. הרשימה רועננה – בדקי את הסטטוס ונסי שוב.';
