import { AuthResponse, CurrentUser, Role } from '../../models/auth.models';

/** Test data only – not imported by application code. */
export function fakeUser(overrides: Partial<CurrentUser> = {}): CurrentUser {
  return {
    id: 7,
    employeeCode: 'E1007',
    firstName: 'דנה',
    lastName: 'לוי',
    email: 'dana@example.test',
    jobTitle: 'רכזת',
    department: { id: 1, name: 'משאבי אנוש' },
    roles: ['Employee'] as Role[],
    mustChangePassword: false,
    ...overrides,
  };
}

export function fakeAuthResponse(accessToken = 'access-token-1', mustChangePassword = false): AuthResponse {
  return {
    accessToken,
    expiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
    mustChangePassword,
    user: fakeUser({ mustChangePassword }),
  };
}

export const UNAUTHORIZED = { status: 401, statusText: 'Unauthorized' } as const;

export const PROBLEM_401 = {
  type: 'https://example.test/errors/invalid-credentials',
  title: 'נדרשת התחברות למערכת',
  status: 401,
};
