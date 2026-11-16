import { dueCountdown, duration, isOverdueNow } from './due-countdown';

describe('dueCountdown', () => {
  const now = new Date('2026-11-16T10:00:00Z');
  const inHours = (hours: number) => new Date(now.getTime() + hours * 3_600_000).toISOString();

  it('before the due time: what is left', () => {
    expect(dueCountdown(inHours(3), 'InProgress', false, now)).toEqual({ overdue: false, text: 'נותרו 3 שעות' });
    expect(dueCountdown(inHours(1.5), 'Assigned', false, now)!.text).toBe('נותרה שעה');
    expect(dueCountdown(inHours(2), 'New', false, now)!.text).toBe('נותרו שעתיים');
    expect(dueCountdown(inHours(0.5), 'New', false, now)!.text).toBe('נותרו 30 דקות');
    expect(dueCountdown(inHours(30), 'New', false, now)!.text).toBe('נותרו 30 שעות');
    expect(dueCountdown(inHours(72), 'New', false, now)!.text).toBe('נותרו 3 ימים');
  });

  it('after the due time: overdue by how long', () => {
    expect(dueCountdown(inHours(-2), 'InProgress', false, now)).toEqual({ overdue: true, text: 'חריגה של שעתיים' });
    expect(dueCountdown(inHours(-49), 'New', false, now)!.text).toBe('חריגה של יומיים');
  });

  it('the server said overdue: overdue even if the local clock is a little behind', () => {
    expect(dueCountdown(inHours(0.01), 'InProgress', true, now)!.overdue).toBeTrue();
  });

  it('not shown while waiting for the employee, once resolved / closed / cancelled, or without a due time', () => {
    for (const status of ['WaitingForEmployee', 'Resolved', 'Closed', 'Cancelled'] as const) {
      expect(dueCountdown(inHours(-5), status, true, now)).toBeNull();
      expect(isOverdueNow(inHours(-5), status, true, now)).toBeFalse();
    }
    expect(dueCountdown(null, 'New', false, now)).toBeNull();
  });

  it('turns overdue when the clock passes the due time', () => {
    const due = inHours(0.5);
    expect(isOverdueNow(due, 'InProgress', false, now)).toBeFalse();
    expect(isOverdueNow(due, 'InProgress', false, new Date(now.getTime() + 31 * 60_000))).toBeTrue();
  });

  it('duration: dual forms and at least a minute', () => {
    expect(duration(10_000)).toBe('דקה');
    expect(duration(2 * 60_000)).toBe('שתי דקות');
    expect(duration(24 * 3_600_000)).toBe('24 שעות');
    expect(duration(48 * 3_600_000)).toBe('יומיים');
    expect(duration(24 * 3_600_000 * 1.5)).toBe('36 שעות');
  });
});
