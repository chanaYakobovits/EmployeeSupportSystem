/** Numbers of the dashboards in Hebrew (spec 11.6). Pure. */

const NUMBER = new Intl.NumberFormat('he-IL');
const PERCENT = new Intl.NumberFormat('he-IL', { maximumFractionDigits: 1 });

/** Shown for a metric without a base (e.g. no ticket resolved in the range). */
export const NO_VALUE = '—';

export function formatNumber(value: number): string {
  return NUMBER.format(value);
}

/** 0–100 with up to one decimal: "87.5%". null → "—". */
export function formatPercent(value: number | null): string {
  return value === null ? NO_VALUE : `${PERCENT.format(value)}%`;
}

function days(n: number): string {
  return n === 1 ? 'יום' : n === 2 ? 'יומיים' : `${n} ימים`;
}

function hours(n: number): string {
  return n === 1 ? 'שעה' : n === 2 ? 'שעתיים' : `${n} שעות`;
}

function minutes(n: number): string {
  // As duration() of the countdowns (services/due-countdown.ts).
  return n === 0 ? 'פחות מדקה' : n === 1 ? 'דקה' : n === 2 ? 'שתי דקות' : `${n} דקות`;
}

/** "ו" before a word, "ו-" before a number: "יום ו-4 שעות", "יומיים ושעה". */
function and(text: string): string {
  return /^\d/.test(text) ? `ו-${text}` : `ו${text}`;
}

/**
 * A duration in hours (the average resolution time) as words: "45 דקות", "שעתיים", "יום ו-4 שעות", "3 ימים".
 * Under an hour → minutes; from an hour → rounded to whole hours (days of 24 hours). null → "—".
 */
export function formatDuration(totalHours: number | null): string {
  if (totalHours === null || !Number.isFinite(totalHours) || totalHours < 0) {
    return NO_VALUE;
  }
  const totalMinutes = Math.round(totalHours * 60);
  if (totalMinutes < 60) {
    return minutes(totalMinutes);
  }
  const rounded = Math.round(totalHours);
  const d = Math.floor(rounded / 24);
  const h = rounded % 24;
  if (d === 0) {
    return hours(h);
  }
  return h === 0 ? days(d) : `${days(d)} ${and(hours(h))}`;
}

const DAY_MONTH = /^(\d{4})-(\d{2})-(\d{2})$/;

/** yyyy-MM-dd → dd/MM (the trend's axis). */
export function formatDayMonth(day: string): string {
  const match = DAY_MONTH.exec(day);
  return match ? `${match[3]}/${match[2]}` : day;
}

/** yyyy-MM-dd → dd/MM/yyyy. */
export function formatDay(day: string): string {
  const match = DAY_MONTH.exec(day);
  return match ? `${match[3]}/${match[2]}/${match[1]}` : day;
}
