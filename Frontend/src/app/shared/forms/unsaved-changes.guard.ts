import { inject } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';

import { ConfirmService } from '../components/confirm-dialog/confirm.service';

/** A screen with a form that would lose what the user typed when they leave. */
export interface HasUnsavedChanges {
  hasUnsavedChanges(): boolean;
}

/**
 * Asks before leaving a screen with unsaved text – cancel, the menu, "back" (spec 7.4).
 * Nothing typed (or already sent) → leaves without asking.
 */
export const unsavedChangesGuard: CanDeactivateFn<HasUnsavedChanges> = (component) => {
  if (!component.hasUnsavedChanges()) {
    return true;
  }
  return inject(ConfirmService).confirm({
    heading: 'יציאה בלי לשלוח',
    message: 'הטקסט שהזנת לא נשמר ויימחק.',
    confirmText: 'יציאה בלי לשלוח',
    cancelText: 'המשך עריכה',
  });
};
