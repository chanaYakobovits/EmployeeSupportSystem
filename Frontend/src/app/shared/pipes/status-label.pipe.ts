import { Pipe, PipeTransform } from '@angular/core';

import { TicketStatus } from '../../core/models/ticket.models';

/** Hebrew ticket status names (spec 3.2). */
export const TICKET_STATUS_LABELS: Readonly<Record<TicketStatus, string>> = {
  New: 'חדשה',
  Assigned: 'שויכה',
  InProgress: 'בטיפול',
  WaitingForEmployee: 'ממתינה לעובד',
  Resolved: 'נפתרה',
  Closed: 'סגורה',
  Cancelled: 'בוטלה',
};

export function statusLabel(status: TicketStatus | null | undefined): string {
  return status ? (TICKET_STATUS_LABELS[status] ?? status) : '';
}

@Pipe({ name: 'statusLabel' })
export class StatusLabelPipe implements PipeTransform {
  transform(status: TicketStatus | null | undefined): string {
    return statusLabel(status);
  }
}
