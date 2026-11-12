import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

import { SlaField, URGENCIES, Urgency, slaField } from '../../../core/models/sla.models';
import { urgencyLabel } from '../../../shared/pipes/urgency-label.pipe';

/** Error key on the less urgent level when it is shorter than the level above it. */
export const SLA_ORDER_ERROR = 'slaOrder';

/** Same wording as the server (UpdateSlaPoliciesRequestValidator.OrderMessage). */
export function slaOrderMessage(level: Urgency, moreUrgent: Urgency): string {
  return `היעד לדחיפות ${urgencyLabel(level)} לא יכול להיות קצר מהיעד לדחיפות ${urgencyLabel(moreUrgent)}`;
}

/**
 * Group validator: Critical ≤ High ≤ Medium ≤ Low (spec 3.4, as on the server). Each violation is put on the less
 * urgent control – the one to fix – as { slaOrder: message }, and removed again once the order is right.
 * Pairs with a missing or invalid value are skipped: that control already shows its own error.
 */
export const slaOrderValidator: ValidatorFn = (group: AbstractControl): ValidationErrors | null => {
  let valid = true;
  for (let i = 1; i < URGENCIES.length; i++) {
    const level = URGENCIES[i];
    const moreUrgent = URGENCIES[i - 1];
    const control = group.get(slaField(level));
    const above = group.get(slaField(moreUrgent));
    if (!control || !above) {
      continue;
    }
    const outOfOrder = isHours(control.value) && isHours(above.value) && control.value < above.value;
    setOrderError(control, outOfOrder ? slaOrderMessage(level, moreUrgent) : null);
    valid &&= !outOfOrder;
  }
  return valid ? null : { [SLA_ORDER_ERROR]: true };
};

function isHours(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value >= 1;
}

function setOrderError(control: AbstractControl, message: string | null): void {
  const { [SLA_ORDER_ERROR]: _current, ...others } = control.errors ?? {};
  const next = message ? { ...others, [SLA_ORDER_ERROR]: message } : others;
  control.setErrors(Object.keys(next).length ? next : null, { emitEvent: false });
}

/**
 * Hours as a readable duration: 72 → "72 שעות = 3 ימים", 30 → "30 שעות = יום ו-6 שעות", 4 → "4 שעות".
 * Calendar hours (spec 3.4), so a day is 24 hours.
 */
export function formatHours(hours: number | null | undefined): string {
  if (typeof hours !== 'number' || !Number.isInteger(hours) || hours < 1) {
    return '';
  }
  const total = hours === 1 ? 'שעה אחת' : `${hours} שעות`;
  if (hours < 24) {
    return total;
  }
  const days = Math.floor(hours / 24);
  const rest = hours % 24;
  const dayText = days === 1 ? 'יום' : days === 2 ? 'יומיים' : `${days} ימים`;
  if (rest === 0) {
    return `${total} = ${days === 1 ? 'יום אחד' : dayText}`;
  }
  return `${total} = ${dayText} ${rest === 1 ? 'ושעה' : `ו-${rest} שעות`}`;
}

/** The SLA form fields, most urgent first. */
export const SLA_LEVELS: readonly { urgency: Urgency; field: SlaField }[] = URGENCIES.map((urgency) => ({
  urgency,
  field: slaField(urgency),
}));
