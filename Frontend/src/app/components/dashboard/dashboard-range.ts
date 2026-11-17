import { ParamMap, Params } from '@angular/router';

import { DASHBOARD_LIMITS, DashboardRangeQuery } from '../../models/dashboard.models';
import { dateParam } from '../../services/list-state';

/**
 * The date range of a dashboard (spec 7.9) and its mapping to the URL: ?days=7|90, or ?from=…&to=… for a custom range.
 * Nothing in the URL = the default 30 days (not written). Pure functions; days are calendar days in Israel (yyyy-MM-dd).
 */

export const PRESET_DAYS = [7, 30, 90] as const;
export type PresetDays = (typeof PRESET_DAYS)[number];

export type DashboardRangeChoice = { kind: 'preset'; days: PresetDays } | { kind: 'custom'; from: string; to: string };

/** The server's default range (DashboardLimits.DefaultRangeDays) – sent as nothing. */
export const DEFAULT_RANGE_DAYS: PresetDays = DASHBOARD_LIMITS.defaultRangeDays;

export const DEFAULT_RANGE: DashboardRangeChoice = { kind: 'preset', days: DEFAULT_RANGE_DAYS };

/** The query params the range owns – cleared before writing a new one. */
export const RANGE_PARAM_KEYS = ['days', 'from', 'to'] as const;

// The same as the server's DashboardRangeQueryValidator.
export const FROM_AFTER_TO_MESSAGE = 'תאריך ההתחלה חייב להיות לפני תאריך הסיום או שווה לו';
export const RANGE_TOO_LONG_MESSAGE = `טווח התאריכים: עד ${DASHBOARD_LIMITS.maxRangeDays} ימים`;
export const RANGE_REQUIRED_MESSAGE = 'יש לבחור תאריך התחלה ותאריך סיום';

const ISRAEL_DAY = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'Asia/Jerusalem',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
});

/** Today in Israel (yyyy-MM-dd), whatever the browser's time zone. */
export function israelToday(now: Date = new Date()): string {
  return ISRAEL_DAY.format(now);
}

/** A calendar day plus n days (n may be negative). */
export function addDays(day: string, n: number): string {
  const date = new Date(`${day}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + n);
  return date.toISOString().slice(0, 10);
}

/** The number of days from → to, both included. */
export function rangeLength(from: string, to: string): number {
  return Math.round((Date.parse(`${to}T00:00:00Z`) - Date.parse(`${from}T00:00:00Z`)) / 86_400_000) + 1;
}

/** The message of an invalid custom range, or null. */
export function customRangeError(from: string | null, to: string | null): string | null {
  if (!from || !to || dateParam.parse(from) === null || dateParam.parse(to) === null) {
    return RANGE_REQUIRED_MESSAGE;
  }
  if (from > to) {
    return FROM_AFTER_TO_MESSAGE;
  }
  return rangeLength(from, to) > DASHBOARD_LIMITS.maxRangeDays ? RANGE_TOO_LONG_MESSAGE : null;
}

/** The range in the URL. Anything invalid → the default. */
export function rangeFromParams(params: ParamMap): DashboardRangeChoice {
  const from = params.get('from');
  const to = params.get('to');
  if (from !== null || to !== null) {
    return from && to && customRangeError(from, to) === null ? { kind: 'custom', from, to } : DEFAULT_RANGE;
  }
  const days = Number(params.get('days'));
  return (PRESET_DAYS as readonly number[]).includes(days) ? { kind: 'preset', days: days as PresetDays } : DEFAULT_RANGE;
}

/** The query params of a range (merged into the current ones). The default → all removed. */
export function rangeToParams(range: DashboardRangeChoice): Params {
  const cleared: Params = Object.fromEntries(RANGE_PARAM_KEYS.map((key) => [key, null]));
  if (range.kind === 'custom') {
    return { ...cleared, from: range.from, to: range.to };
  }
  return range.days === DEFAULT_RANGE_DAYS ? cleared : { ...cleared, days: range.days };
}

/** What the API receives: a preset other than the default → the days up to today in Israel; the default → nothing. */
export function rangeQuery(range: DashboardRangeChoice, now: Date = new Date()): DashboardRangeQuery {
  if (range.kind === 'custom') {
    return { from: range.from, to: range.to };
  }
  if (range.days === DEFAULT_RANGE_DAYS) {
    return {};
  }
  const to = israelToday(now);
  return { from: addDays(to, -(range.days - 1)), to };
}

export function sameRange(a: DashboardRangeChoice, b: DashboardRangeChoice): boolean {
  return a.kind === 'preset'
    ? b.kind === 'preset' && a.days === b.days
    : b.kind === 'custom' && a.from === b.from && a.to === b.to;
}
