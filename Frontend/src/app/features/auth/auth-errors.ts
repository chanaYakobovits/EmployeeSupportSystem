import { HttpErrorResponse } from '@angular/common/http';

import { toUserMessage } from '../../core/interceptors/error.interceptor';
import { isProblemDetails, problemType } from '../../core/models/problem-details';

/** An error shown inside an auth form (instead of a toast). */
export interface FormAlert {
  title: string;
  detail?: string;
}

const INVALID_CREDENTIALS = 'פרטי ההתחברות שגויים';
const TOO_MANY_ATTEMPTS = 'בוצעו יותר מדי ניסיונות. יש להמתין מעט ולנסות שוב.';

/** Spec 4.1 / 7.1: one uniform message for every failure; the lockout message includes the waiting time. */
export function loginAlert(error: unknown): FormAlert {
  if (!(error instanceof HttpErrorResponse)) {
    return { title: toUserMessage(new HttpErrorResponse({ status: 500 })) };
  }
  if (error.status === 401) {
    if (problemType(error) === 'account-locked' && isProblemDetails(error.error)) {
      return { title: error.error.title ?? INVALID_CREDENTIALS, detail: error.error.detail };
    }
    return { title: INVALID_CREDENTIALS };
  }
  if (error.status === 429) {
    return { title: TOO_MANY_ATTEMPTS };
  }
  return { title: toUserMessage(error) };
}

/** Errors of POST /auth/change-password that are not tied to a single field. */
export function changePasswordAlert(error: unknown): FormAlert {
  if (!(error instanceof HttpErrorResponse)) {
    return { title: toUserMessage(new HttpErrorResponse({ status: 500 })) };
  }
  if (error.status === 429) {
    return { title: TOO_MANY_ATTEMPTS };
  }
  return { title: toUserMessage(error) };
}
