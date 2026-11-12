import { Pipe, PipeTransform } from '@angular/core';

/** Dates are shown in Israel time (spec 11.6), whatever the browser's time zone. */
const DISPLAY_TIME_ZONE = 'Asia/Jerusalem';

const FORMATTER = new Intl.DateTimeFormat('en-GB', {
  timeZone: DISPLAY_TIME_ZONE,
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

/**
 * dd/MM/yyyy HH:mm in Israel time (spec 11.6), or '' for no value.
 * The API sends UTC ("…Z"); a value without a zone is read as UTC too, never as browser-local time.
 */
export function formatDateTime(value: string | Date | null | undefined): string {
  if (value === null || value === undefined || value === '') {
    return '';
  }
  const date = typeof value === 'string' ? new Date(hasZone(value) ? value : `${value}Z`) : value;
  if (Number.isNaN(date.getTime())) {
    return '';
  }
  const parts = Object.fromEntries(FORMATTER.formatToParts(date).map((part) => [part.type, part.value]));
  return `${parts['day']}/${parts['month']}/${parts['year']} ${parts['hour']}:${parts['minute']}`;
}

function hasZone(value: string): boolean {
  return /(Z|[+-]\d{2}:?\d{2})$/i.test(value) || !value.includes('T');
}

@Pipe({ name: 'dateTime' })
export class DateTimePipe implements PipeTransform {
  transform(value: string | Date | null | undefined): string {
    return formatDateTime(value);
  }
}
