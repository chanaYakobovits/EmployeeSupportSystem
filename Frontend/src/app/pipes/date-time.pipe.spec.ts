import { formatDateTime } from './date-time.pipe';

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
