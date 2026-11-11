import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';

import { refreshesOn401 } from '../http/api-request';
import { SKIP_ERROR_TOAST } from '../http/http-context';
import { isProblemDetails } from '../models/problem-details';
import { NotificationService } from '../notifications/notification.service';

const NETWORK_ERROR_MESSAGE = 'לא ניתן להתחבר לשרת. יש לוודא שהשרת פועל ולנסות שוב.';

const FALLBACK_MESSAGES: Record<number, string> = {
  400: 'הבקשה אינה תקינה',
  401: 'נדרשת התחברות למערכת',
  403: 'אין לך הרשאה לבצע פעולה זו',
  404: 'הפריט המבוקש לא נמצא',
  409: 'הנתונים עודכנו בינתיים, יש לרענן ולנסות שוב',
  422: 'לא ניתן לבצע את הפעולה',
  429: 'בוצעו יותר מדי בקשות, יש לנסות שוב בעוד מספר דקות',
};

const GENERIC_MESSAGE = 'אירעה שגיאה בלתי צפויה. יש לנסות שוב מאוחר יותר.';

/**
 * Shows the server's ProblemDetails title (Hebrew) in a toast, then re-throws
 * so the calling component can still react (e.g. show an error state / reload on 409).
 * No toast when the request opted out (SKIP_ERROR_TOAST – the screen shows the error itself)
 * or for a 401 that auth.interceptor resolves by refreshing the token.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const notifications = inject(NotificationService);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && shouldNotify(req, error)) {
        notifications.error(toUserMessage(error));
      }
      return throwError(() => error);
    }),
  );
};

function shouldNotify(req: HttpRequest<unknown>, error: HttpErrorResponse): boolean {
  if (req.context.get(SKIP_ERROR_TOAST)) {
    return false;
  }
  return !(error.status === 401 && refreshesOn401(req));
}

export function toUserMessage(error: HttpErrorResponse): string {
  // status 0: the request never reached the server (server down, CORS blocked, offline).
  if (error.status === 0) {
    return NETWORK_ERROR_MESSAGE;
  }
  if (isProblemDetails(error.error) && error.error.title) {
    return error.error.title;
  }
  return FALLBACK_MESSAGES[error.status] ?? GENERIC_MESSAGE;
}
