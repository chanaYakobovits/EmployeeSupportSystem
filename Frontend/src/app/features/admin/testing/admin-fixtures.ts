import { HttpErrorResponse } from '@angular/common/http';

import { Category } from '../../../core/models/category.models';
import { Department } from '../../../core/models/department.models';
import { UserDetails } from '../../../core/models/user.models';

/** Test data only – not imported by application code. */
export function fakeUserDetails(overrides: Partial<UserDetails> = {}): UserDetails {
  return {
    id: 42,
    employeeCode: 'EMP-00042',
    firstName: 'יוסי',
    lastName: 'כהן',
    email: 'yossi@example.test',
    jobTitle: 'רכז שכר',
    department: { id: 2, name: 'כספים' },
    roles: ['Employee'],
    isActive: true,
    createdAt: '2026-10-01T08:00:00Z',
    lastLoginAt: null,
    mustChangePassword: false,
    isLockedOut: false,
    managedDepartment: null,
    invitation: null,
    ...overrides,
  };
}

export function fakeDepartment(overrides: Partial<Department> = {}): Department {
  return {
    id: 2,
    name: 'כספים',
    isActive: true,
    manager: null,
    activeUserCount: 4,
    handlerCount: 1,
    ...overrides,
  };
}

export function fakeCategory(overrides: Partial<Category> = {}): Category {
  return {
    id: 1,
    code: 'PAYROLL',
    name: 'שכר ותלושים',
    description: 'טעות בתלוש, שעות נוספות שלא שולמו',
    routingType: 'FixedDepartment',
    handlingDepartment: { id: 3, name: 'הנהלת חשבונות' },
    isFallback: false,
    isActive: true,
    ...overrides,
  };
}

/** An error response as the API returns it (ProblemDetails). */
export function problemResponse(
  status: number,
  type: string,
  title: string,
  extra: { detail?: string; errors?: Record<string, string[]> } = {},
): HttpErrorResponse {
  return new HttpErrorResponse({
    status,
    error: { status, type: `https://example.test/errors/${type}`, title, ...extra },
  });
}
