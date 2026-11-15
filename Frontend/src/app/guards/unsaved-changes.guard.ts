import { inject } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';

import { ConfirmService } from '../services/confirm.service';

/** A screen with a form that would lose what the user typed or chose when they leave. */
export interface HasUnsavedChanges {
  hasUnsavedChanges(): boolean;
  /** What would be lost, for the confirm dialog. Default: UNSAVED_TEXT_MESSAGE. */
  unsavedChangesMessage?(): string;
}

export const UNSAVED_TEXT_MESSAGE = 'הטקסט שהזנת לא נשמר ויימחק.';

/** The confirm message for typed text, chosen files, or both. */
export function unsavedMessage(hasText: boolean, hasFiles: boolean): string {
  if (hasText && hasFiles) {
    return 'הטקסט והקבצים שבחרת לא נשמרו ויימחקו.';
  }
  return hasFiles ? 'הקבצים שבחרת לא נשלחו.' : UNSAVED_TEXT_MESSAGE;
}

/**
 * Asks before leaving a screen with unsaved text or files – cancel, the menu, "back" (spec 7.4).
 * Nothing typed or chosen (or already sent) → leaves without asking.
 */
export const unsavedChangesGuard: CanDeactivateFn<HasUnsavedChanges> = (component) => {
  if (!component.hasUnsavedChanges()) {
    return true;
  }
  return inject(ConfirmService).confirm({
    heading: 'יציאה בלי לשלוח',
    message: component.unsavedChangesMessage?.() ?? UNSAVED_TEXT_MESSAGE,
    confirmText: 'יציאה בלי לשלוח',
    cancelText: 'המשך עריכה',
  });
};
