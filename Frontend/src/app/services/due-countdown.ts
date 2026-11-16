import { parseApiDate } from '../pipes/date-time.pipe';
import { OPEN_SLA_STATUSES, TicketStatus } from '../models/ticket.models';

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

export interface DueCountdown {
  /** Past the due time. */
  overdue: boolean;
  /** "נותרו 3 שעות" / "חריגה של שעתיים". */
  text: string;
}

/**
 * The countdown to the SLA due time (task 3.4), or null when it is not shown: no due time, or a status whose SLA clock
 * does not run (WaitingForEmployee – paused; resolved, closed, cancelled – done). Same statuses as the server's
 * TicketStatuses.Open.
 * Overdue: the server said so when the data was loaded, or the due time has passed since (the clock moves on).
 * Under an hour → minutes; under 48 hours → hours; otherwise days (whole units, rounded down; at least one minute).
 */
export function dueCountdown(
  dueAt: string | null,
  status: TicketStatus,
  isOverdue: boolean,
  now: Date,
): DueCountdown | null {
  const due = parseApiDate(dueAt);
  if (due === null || !OPEN_SLA_STATUSES.includes(status)) {
    return null;
  }
  const remaining = due.getTime() - now.getTime();
  const overdue = isOverdue || remaining < 0;
  const span = duration(Math.abs(remaining));
  return overdue ? { overdue, text: `חריגה של ${span}` } : { overdue, text: `${remainingVerb(span)} ${span}` };
}

/** Agreement with the singular: "נותרה שעה", "נותר יום", "נותרו 3 שעות". */
function remainingVerb(span: string): string {
  if (span === 'שעה' || span === 'דקה') {
    return 'נותרה';
  }
  return span === 'יום' ? 'נותר' : 'נותרו';
}

/** True when the row is past its SLA now (the server's isOverdue, or the due time passed since the list was loaded). */
export function isOverdueNow(dueAt: string | null, status: TicketStatus, isOverdue: boolean, now: Date): boolean {
  return dueCountdown(dueAt, status, isOverdue, now)?.overdue ?? false;
}

/** "3 שעות", "שעתיים", "יום" – Hebrew has a dual form (written by hand, as formatRelativeTime). */
export function duration(ms: number): string {
  if (ms < HOUR) {
    return count(Math.max(1, Math.floor(ms / MINUTE)), 'דקה', 'שתי דקות', 'דקות');
  }
  if (ms < 2 * DAY) {
    return count(Math.floor(ms / HOUR), 'שעה', 'שעתיים', 'שעות');
  }
  return count(Math.floor(ms / DAY), 'יום', 'יומיים', 'ימים');
}

function count(value: number, one: string, two: string, many: string): string {
  if (value === 1) {
    return one;
  }
  if (value === 2) {
    return two;
  }
  return `${value} ${many}`;
}
