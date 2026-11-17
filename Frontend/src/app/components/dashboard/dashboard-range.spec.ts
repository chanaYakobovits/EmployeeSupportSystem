import { convertToParamMap } from '@angular/router';

import {
  DEFAULT_RANGE,
  FROM_AFTER_TO_MESSAGE,
  RANGE_REQUIRED_MESSAGE,
  RANGE_TOO_LONG_MESSAGE,
  addDays,
  customRangeError,
  israelToday,
  rangeFromParams,
  rangeQuery,
  rangeToParams,
} from './dashboard-range';

describe('dashboard-range', () => {
  it('"today" is the day in Israel, whatever the browser’s zone', () => {
    // 22:30 UTC on 16/11 is already 17/11 in Israel (UTC+2 in November).
    expect(israelToday(new Date('2026-11-16T22:30:00Z'))).toBe('2026-11-17');
    expect(israelToday(new Date('2026-11-16T21:30:00Z'))).toBe('2026-11-16');
  });

  it('addDays crosses months', () => {
    expect(addDays('2026-11-01', -1)).toBe('2026-10-31');
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01');
  });

  it('reads the URL: days, a custom range, and anything invalid as the default 30 days', () => {
    expect(rangeFromParams(convertToParamMap({}))).toEqual(DEFAULT_RANGE);
    expect(rangeFromParams(convertToParamMap({ days: '7' }))).toEqual({ kind: 'preset', days: 7 });
    expect(rangeFromParams(convertToParamMap({ days: '90' }))).toEqual({ kind: 'preset', days: 90 });
    expect(rangeFromParams(convertToParamMap({ days: '14' }))).toEqual(DEFAULT_RANGE);
    expect(rangeFromParams(convertToParamMap({ from: '2026-01-01', to: '2026-01-31' }))).toEqual({
      kind: 'custom',
      from: '2026-01-01',
      to: '2026-01-31',
    });
    expect(rangeFromParams(convertToParamMap({ from: '2026-02-01', to: '2026-01-01' }))).toEqual(DEFAULT_RANGE);
    expect(rangeFromParams(convertToParamMap({ from: '2026-01-01' }))).toEqual(DEFAULT_RANGE);
  });

  it('writes the URL: the default is not written, the others replace each other', () => {
    expect(rangeToParams(DEFAULT_RANGE)).toEqual({ days: null, from: null, to: null });
    expect(rangeToParams({ kind: 'preset', days: 7 })).toEqual({ days: 7, from: null, to: null });
    expect(rangeToParams({ kind: 'custom', from: '2026-01-01', to: '2026-01-31' })).toEqual({
      days: null,
      from: '2026-01-01',
      to: '2026-01-31',
    });
  });

  it('asks the API: 30 → nothing (the server’s default); 7 / 90 → the days up to today in Israel; custom as is', () => {
    const now = new Date('2026-11-17T10:00:00Z');
    expect(rangeQuery(DEFAULT_RANGE, now)).toEqual({});
    expect(rangeQuery({ kind: 'preset', days: 7 }, now)).toEqual({ from: '2026-11-11', to: '2026-11-17' });
    expect(rangeQuery({ kind: 'preset', days: 90 }, now)).toEqual({ from: '2026-08-20', to: '2026-11-17' });
    expect(rangeQuery({ kind: 'custom', from: '2026-01-01', to: '2026-01-31' }, now)).toEqual({
      from: '2026-01-01',
      to: '2026-01-31',
    });
  });

  it('a custom range: both days, from ≤ to, at most 366 days (as the server)', () => {
    expect(customRangeError(null, '2026-01-01')).toBe(RANGE_REQUIRED_MESSAGE);
    expect(customRangeError('2026-02-01', '2026-01-01')).toBe(FROM_AFTER_TO_MESSAGE);
    expect(customRangeError('2024-01-01', '2024-12-31')).toBeNull(); // 366 days, a leap year
    expect(customRangeError('2025-01-01', '2026-01-02')).toBe(RANGE_TOO_LONG_MESSAGE);
    expect(customRangeError('2026-01-05', '2026-01-05')).toBeNull();
  });
});
