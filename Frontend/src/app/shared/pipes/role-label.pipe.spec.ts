import { RoleLabelPipe, primaryRole } from './role-label.pipe';

describe('RoleLabelPipe', () => {
  const pipe = new RoleLabelPipe();

  it('translates every role to Hebrew', () => {
    expect(pipe.transform('Employee')).toBe('עובד');
    expect(pipe.transform('Handler')).toBe('גורם מטפל');
    expect(pipe.transform('DepartmentManager')).toBe('מנהל/ת מחלקה');
    expect(pipe.transform('Executive')).toBe('מנכ"ל');
    expect(pipe.transform('Admin')).toBe('מנהל/ת מערכת');
  });

  it('returns an empty string for no role', () => {
    expect(pipe.transform(null)).toBe('');
  });
});

describe('primaryRole', () => {
  it('picks the most senior role', () => {
    expect(primaryRole(['Employee', 'Handler', 'DepartmentManager', 'Admin'])).toBe('Admin');
    expect(primaryRole(['Employee', 'Executive'])).toBe('Executive');
    expect(primaryRole(['Employee', 'Handler'])).toBe('Handler');
    expect(primaryRole(['Employee'])).toBe('Employee');
  });
});
