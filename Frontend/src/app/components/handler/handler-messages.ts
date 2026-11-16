/** Spec 4.4 / 11.2: the wording of a concurrency conflict (409), shown after the data was reloaded. */
export const CONFLICT_MESSAGE = 'הפנייה עודכנה על ידי משתמש אחר. הנתונים רועננו – בדקי ונסי שוב.';

/** After a change that moved the ticket out of the user's scopes (TicketUpdated.canView false). */
export const LEFT_SCOPE_MESSAGE = 'הפנייה הועברה ואינה עוד בטיפולך. חזרת לתור הפניות.';

export const TAKEN_MESSAGE = 'הפנייה שויכה אלייך';
export const TAKE_FAILED_MESSAGE = 'לא ניתן לקחת את הפנייה לטיפול. הרשימה רועננה.';
