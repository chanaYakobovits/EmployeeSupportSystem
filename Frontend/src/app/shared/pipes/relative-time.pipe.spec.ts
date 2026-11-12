import { formatRelativeTime } from './relative-time.pipe';

describe('formatRelativeTime', () => {
  const now = new Date('2026-11-12T10:00:00Z');
  const ago = (ms: number) => new Date(now.getTime() - ms).toISOString();
  const MINUTE = 60_000;
  const HOUR = 60 * MINUTE;
  const DAY = 24 * HOUR;

  it('under a minute (or slightly in the future) → "עכשיו"', () => {
    expect(formatRelativeTime(ago(20_000), now)).toBe('עכשיו');
    expect(formatRelativeTime(ago(-5_000), now)).toBe('עכשיו');
  });

  it('minutes, hours and days in Hebrew', () => {
    expect(formatRelativeTime(ago(MINUTE), now)).toBe('לפני דקה');
    expect(formatRelativeTime(ago(5 * MINUTE), now)).toBe('לפני 5 דקות');
    expect(formatRelativeTime(ago(HOUR), now)).toBe('לפני שעה');
    expect(formatRelativeTime(ago(2 * HOUR + 10 * MINUTE), now)).toBe('לפני שעתיים');
    expect(formatRelativeTime(ago(7 * HOUR), now)).toBe('לפני 7 שעות');
    expect(formatRelativeTime(ago(DAY + HOUR), now)).toBe('אתמול');
    expect(formatRelativeTime(ago(2 * DAY), now)).toBe('לפני יומיים');
    expect(formatRelativeTime(ago(3 * DAY), now)).toBe('לפני 3 ימים');
  });

  it('after 30 days: the full date in Israel time', () => {
    expect(formatRelativeTime('2026-09-01T09:30:00Z', now)).toBe('01/09/2026 12:30');
  });

  it('no value → empty', () => {
    expect(formatRelativeTime(null, now)).toBe('');
    expect(formatRelativeTime('not a date', now)).toBe('');
  });
});
