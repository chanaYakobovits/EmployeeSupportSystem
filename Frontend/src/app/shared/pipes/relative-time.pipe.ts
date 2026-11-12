import { Pipe, PipeTransform } from '@angular/core';

import { formatDateTime, parseApiDate } from './date-time.pipe';

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;
/** Older than this: the full date is clearer than "לפני 5 חודשים". */
const MAX_RELATIVE = 30 * DAY;

/**
 * "לפני שעתיים", "אתמול", "לפני 3 ימים" – or the full date (dd/MM/yyyy HH:mm, Israel time) after 30 days.
 * Under a minute (or a time slightly in the future, from clock skew) → "עכשיו". '' for no value.
 */
export function formatRelativeTime(value: string | Date | null | undefined, now: Date = new Date()): string {
  const date = parseApiDate(value);
  if (date === null) {
    return '';
  }
  const elapsed = now.getTime() - date.getTime();
  if (elapsed < MINUTE) {
    return 'עכשיו';
  }
  if (elapsed < HOUR) {
    return ago(Math.floor(elapsed / MINUTE), 'דקה', 'שתי דקות', 'דקות');
  }
  if (elapsed < DAY) {
    return ago(Math.floor(elapsed / HOUR), 'שעה', 'שעתיים', 'שעות');
  }
  if (elapsed <= MAX_RELATIVE) {
    const days = Math.floor(elapsed / DAY);
    return days === 1 ? 'אתמול' : ago(days, 'יום', 'יומיים', 'ימים');
  }
  return formatDateTime(date);
}

/**
 * Hebrew has a dual form ("שעתיים", "יומיים"). Written by hand: Intl.RelativeTimeFormat('he') gives
 * "לפני שעתיים (2)" in Chrome.
 */
function ago(count: number, one: string, two: string, many: string): string {
  if (count === 1) {
    return `לפני ${one}`;
  }
  if (count === 2) {
    return `לפני ${two}`;
  }
  return `לפני ${count} ${many}`;
}

/** Pure: computed when the value changes (on load / refresh), not on a timer. */
@Pipe({ name: 'relativeTime' })
export class RelativeTimePipe implements PipeTransform {
  transform(value: string | Date | null | undefined): string {
    return formatRelativeTime(value);
  }
}
