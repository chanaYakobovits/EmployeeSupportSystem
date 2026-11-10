import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';

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
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const notifications = inject(NotificationService);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        notifications.error(toUserMessage(error));
      }
      return throwError(() => error);
    }),
  );
};

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
