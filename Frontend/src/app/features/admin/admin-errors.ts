import { HttpErrorResponse } from '@angular/common/http';

import { toUserMessage } from '../../core/interceptors/error.interceptor';
import { isProblemDetails, problemType } from '../../core/models/problem-details';
import { FormAlert } from '../../shared/components/form-alert/form-alert';

export const DEPARTMENTS_URL = '/admin/departments';
export const USERS_URL = '/admin/users';

/**
 * 422 types whose way forward is the departments screen (CLAUDE.md: the DepartmentManager role follows from
 * the department's manager; a manager is replaced there before being deactivated).
 */
const RESOLVED_ON_DEPARTMENTS_SCREEN = new Set([
  'department-manager-deactivation',
  'department-manager-role',
  'manages-other-department',
]);

/** A server error of an admin action, as an alert: the server's Hebrew title and detail, plus a link when useful. */
export function adminAlert(error: unknown): FormAlert {
  if (!(error instanceof HttpErrorResponse)) {
    return { title: toUserMessage(new HttpErrorResponse({ status: 500 })) };
  }
  const alert: FormAlert = { title: toUserMessage(error) };
  if (isProblemDetails(error.error) && error.error.detail) {
    alert.detail = error.error.detail;
  }
  const type = problemType(error);
  if (type && RESOLVED_ON_DEPARTMENTS_SCREEN.has(type)) {
    alert.link = { label: 'מעבר למסך המחלקות', url: DEPARTMENTS_URL };
  }
  return alert;
}

/** "X פניות חזרו לתור המחלקה" after deactivating a handler (CLAUDE.md), or '' for none. */
export function unassignedTicketsMessage(count: number): string {
  if (count <= 0) {
    return '';
  }
  return count === 1 ? 'פנייה אחת חזרה לתור המחלקה.' : `${count} פניות חזרו לתור המחלקה.`;
}
