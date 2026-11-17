import { formatDay, formatDayMonth, formatDuration, formatNumber, formatPercent } from './dashboard-format';

describe('dashboard-format', () => {
  describe('formatDuration (the average resolution time, in hours)', () => {
    it('no value → "—"', () => {
      expect(formatDuration(null)).toBe('—');
      expect(formatDuration(Number.NaN)).toBe('—');
      expect(formatDuration(-1)).toBe('—');
    });

    it('under an hour → minutes, with the Hebrew forms', () => {
      expect(formatDuration(0)).toBe('פחות מדקה');
      expect(formatDuration(1 / 60)).toBe('דקה');
      expect(formatDuration(2 / 60)).toBe('שתי דקות');
      expect(formatDuration(0.75)).toBe('45 דקות');
      expect(formatDuration(0.99)).toBe('59 דקות');
    });

    it('hours: one, the dual form, plural', () => {
      expect(formatDuration(1)).toBe('שעה');
      expect(formatDuration(1.4)).toBe('שעה');
      expect(formatDuration(2)).toBe('שעתיים');
      expect(formatDuration(5.2)).toBe('5 שעות');
      expect(formatDuration(23.4)).toBe('23 שעות');
    });

    it('days and hours: "יום ו-4 שעות", "יומיים ושעה" – rounded to whole hours', () => {
      expect(formatDuration(23.6)).toBe('יום');
      expect(formatDuration(24)).toBe('יום');
      expect(formatDuration(28)).toBe('יום ו-4 שעות');
      expect(formatDuration(26)).toBe('יום ושעתיים');
      expect(formatDuration(49)).toBe('יומיים ושעה');
      expect(formatDuration(48)).toBe('יומיים');
      expect(formatDuration(77)).toBe('3 ימים ו-5 שעות');
      expect(formatDuration(240)).toBe('10 ימים');
    });
  });

  it('percent: up to one decimal, "—" without a base', () => {
    expect(formatPercent(85)).toBe('85%');
    expect(formatPercent(70.5)).toBe('70.5%');
    expect(formatPercent(0)).toBe('0%');
    expect(formatPercent(null)).toBe('—');
  });

  it('numbers and days', () => {
    expect(formatNumber(12345)).toBe('12,345');
    expect(formatDayMonth('2026-11-07')).toBe('07/11');
    expect(formatDay('2026-11-07')).toBe('07/11/2026');
  });
});
