import { formatDate, formatDateTime } from './date-time.pipe';

describe('formatDateTime', () => {
  it('dd/MM/yyyy HH:mm in Israel time (winter UTC+2, summer UTC+3)', () => {
    expect(formatDateTime('2026-11-12T10:05:00Z')).toBe('12/11/2026 12:05');
    expect(formatDateTime('2026-07-01T21:30:00Z')).toBe('02/07/2026 00:30');
  });

  it('reads a value without a zone as UTC', () => {
    expect(formatDateTime('2026-11-12T10:05:00')).toBe('12/11/2026 12:05');
  });

  it('no value → empty', () => {
    expect(formatDateTime(null)).toBe('');
    expect(formatDateTime(undefined)).toBe('');
    expect(formatDateTime('not a date')).toBe('');
  });
});

describe('formatDate', () => {
  it('dd/MM/yyyy in Israel time – the day may differ from UTC', () => {
    expect(formatDate('2026-11-17T22:30:00Z')).toBe('18/11/2026');
    expect(formatDate('2026-11-17T08:00:00Z')).toBe('17/11/2026');
  });

  it('no value: an empty string', () => {
    expect(formatDate(null)).toBe('');
  });
});
