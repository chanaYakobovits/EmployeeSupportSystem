import { HttpContext, HttpContextToken } from '@angular/common/http';

/**
 * Set on a request whose errors the calling screen shows itself (e.g. next to form fields),
 * so error.interceptor does not also show a toast.
 */
export const SKIP_ERROR_TOAST = new HttpContextToken<boolean>(() => false);

/** Marks /auth/* requests: sent with credentials (refresh cookie) and never trigger a token refresh. */
export const AUTH_REQUEST = new HttpContextToken<boolean>(() => false);

/** Set by auth.interceptor on the single retry after a refresh, so a request is never retried twice. */
export const RETRIED_AFTER_REFRESH = new HttpContextToken<boolean>(() => false);

/** Convenience: a context for a request that handles its own errors. */
export function skipErrorToast(context = new HttpContext()): HttpContext {
  return context.set(SKIP_ERROR_TOAST, true);
}
