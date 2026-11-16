import { HttpErrorResponse } from '@angular/common/http';

import { toUserMessage } from '../../../interceptors/error.interceptor';
import { isProblemDetails } from '../../../models/problem-details';
import { isConflictError } from '../../../services/ticket-handle.store';
import { FormAlert } from '../../shared/form-alert/form-alert';
import { CONFLICT_MESSAGE } from '../handler-messages';

/**
 * The alert of a failed change in a dialog (reclassify / transfer): 409 → the wording of spec 11.2 (the data was
 * reloaded, what the user wrote stays); 403 / 422 → the server's Hebrew title (e.g. "הפנייה כבר משויכת ליעד שנבחר");
 * anything else → the general message. Field errors are shown next to the fields, not here.
 */
export function changeErrorAlert(error: unknown, unmatched: readonly string[] = []): FormAlert {
  if (isConflictError(error)) {
    return { title: CONFLICT_MESSAGE, detail: 'מה שכתבת נשמר בחלון.' };
  }
  const httpError = error instanceof HttpErrorResponse ? error : new HttpErrorResponse({ status: 500 });
  const problem = isProblemDetails(httpError.error) ? httpError.error : null;
  // toUserMessage: the server's title when there is one, else a message for the status.
  const alert: FormAlert = { title: toUserMessage(httpError) };
  if (unmatched.length > 0) {
    alert.detail = unmatched.join(' ');
  } else if (problem?.detail) {
    alert.detail = problem.detail;
  }
  return alert;
}
