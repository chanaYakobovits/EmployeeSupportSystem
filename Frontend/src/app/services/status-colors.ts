import { TicketStatus } from '../models/ticket.models';

/**
 * The colour of every ticket status – the one source for the status badge and the dashboard's status chart.
 * The values themselves are CSS variables in styles.scss (--status-*, design guide "צבעי סטטוס").
 */
export const TICKET_STATUS_COLOR_VARS: Readonly<Record<TicketStatus, string>> = {
  New: '--status-new',
  Assigned: '--status-assigned',
  InProgress: '--status-in-progress',
  WaitingForEmployee: '--status-waiting',
  Resolved: '--status-resolved',
  Closed: '--status-closed',
  Cancelled: '--status-cancelled',
};

/** For CSS: var(--status-…). */
export function statusColorVar(status: TicketStatus): string {
  return `var(${TICKET_STATUS_COLOR_VARS[status]})`;
}

/**
 * The resolved value of a CSS variable on :root, for drawing on a canvas (a chart cannot read var()).
 * Empty → the fallback, so a missing token never draws an invisible bar.
 */
export function cssColor(varName: string, fallback = '#666666'): string {
  const value = getComputedStyle(document.documentElement).getPropertyValue(varName).trim();
  return value || fallback;
}
