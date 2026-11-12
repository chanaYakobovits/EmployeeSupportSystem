import { Pipe, PipeTransform } from '@angular/core';

import { Urgency } from '../../core/models/sla.models';

/** Hebrew urgency names (spec 3.4). */
export const URGENCY_LABELS: Readonly<Record<Urgency, string>> = {
  Critical: 'קריטית',
  High: 'גבוהה',
  Medium: 'בינונית',
  Low: 'נמוכה',
};

export function urgencyLabel(urgency: Urgency | null | undefined): string {
  return urgency ? (URGENCY_LABELS[urgency] ?? urgency) : '';
}

@Pipe({ name: 'urgencyLabel' })
export class UrgencyLabelPipe implements PipeTransform {
  transform(urgency: Urgency | null | undefined): string {
    return urgencyLabel(urgency);
  }
}
