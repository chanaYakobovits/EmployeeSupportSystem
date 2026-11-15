/**
 * Notices other screens can leave for the login screen through the navigation state:
 * router.navigate(['/login'], { state: loginNoticeState('passwordSet') }).
 * Only a key travels; the text is here, so nothing else (e.g. a crafted history entry) can make the login screen
 * show arbitrary text. Not in the URL, so it is gone after a reload.
 */
export const LOGIN_NOTICES = {
  passwordSet: 'הסיסמה נקבעה. אפשר להתחבר עם המייל או קוד העובד.',
} as const;

export type LoginNotice = keyof typeof LOGIN_NOTICES;

const STATE_KEY = 'loginNotice';

export function loginNoticeState(notice: LoginNotice): Record<string, string> {
  return { [STATE_KEY]: notice };
}

/** The notice text in a navigation / history state, or null. */
export function readLoginNotice(state: unknown): string | null {
  const key = (state as Record<string, unknown> | null | undefined)?.[STATE_KEY];
  return typeof key === 'string' && Object.hasOwn(LOGIN_NOTICES, key) ? LOGIN_NOTICES[key as LoginNotice] : null;
}
