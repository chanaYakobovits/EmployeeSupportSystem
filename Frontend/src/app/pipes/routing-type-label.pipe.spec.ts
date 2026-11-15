import { RoutingTypeLabelPipe } from './routing-type-label.pipe';

describe('RoutingTypeLabelPipe', () => {
  const pipe = new RoutingTypeLabelPipe();

  it('translates every routing type to Hebrew', () => {
    expect(pipe.transform('FixedDepartment')).toBe('מחלקה מטפלת קבועה');
    expect(pipe.transform('RequesterDepartmentHr')).toBe('HR של מחלקת העובד');
  });

  it('returns an empty string for none', () => {
    expect(pipe.transform(undefined)).toBe('');
  });
});
