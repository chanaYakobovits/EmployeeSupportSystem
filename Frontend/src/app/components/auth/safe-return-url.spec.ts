import { safeReturnUrl } from './safe-return-url';

describe('safeReturnUrl (open-redirect protection)', () => {
  it('accepts an in-app path', () => {
    expect(safeReturnUrl('/employee')).toBe('/employee');
    expect(safeReturnUrl('/handler?page=2&status=New')).toBe('/handler?page=2&status=New');
  });

  for (const unsafe of [
    '//evil.com',
    'https://evil.com',
    'javascript:alert(1)',
    '/\\evil.com',
    '/redirect?to=https://evil.com',
    'employee',
    ' /employee',
  ]) {
    it(`sends "${unsafe}" to the home page`, () => {
      expect(safeReturnUrl(unsafe)).toBe('/');
    });
  }

  it('sends an empty or missing value to the home page', () => {
    expect(safeReturnUrl('')).toBe('/');
    expect(safeReturnUrl(null)).toBe('/');
    expect(safeReturnUrl(undefined)).toBe('/');
  });

  it('never returns to the auth screens', () => {
    expect(safeReturnUrl('/login')).toBe('/');
    expect(safeReturnUrl('/change-password')).toBe('/');
    expect(safeReturnUrl('/account/change-password?returnUrl=/x')).toBe('/');
  });
});
