import { ConfirmDialogData } from '../../shared/confirm-dialog/confirm-dialog';

/** The employee's wording of a concurrency conflict (409) – the data was reloaded. */
export const EMPLOYEE_CONFLICT_MESSAGE = 'הפנייה עודכנה בינתיים. הנתונים נטענו מחדש – אפשר לנסות שוב.';

export const CANCELLED_MESSAGE = 'הפנייה בוטלה';
export const CONFIRMED_MESSAGE = 'הפנייה נסגרה. תודה!';
export const REOPENED_MESSAGE = 'הפנייה נפתחה מחדש';

export const CANCEL_CONFIRM: ConfirmDialogData = {
  heading: 'ביטול הפנייה',
  message: 'לא ניתן לבטל את הביטול – כדי לפנות שוב יהיה צורך לפתוח פנייה חדשה.',
  confirmText: 'ביטול הפנייה',
  cancelText: 'חזרה',
  tone: 'danger',
};

export const CONFIRM_RESOLVED_CONFIRM: ConfirmDialogData = {
  heading: 'הבעיה נפתרה',
  message: 'הפנייה תיסגר סופית ולא ניתן יהיה לפתוח אותה מחדש.',
  confirmText: 'סגירת הפנייה',
  cancelText: 'חזרה',
};
