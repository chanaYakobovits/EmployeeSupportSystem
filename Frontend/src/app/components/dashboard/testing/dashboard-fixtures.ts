import { signal } from '@angular/core';

import { Role } from '../../../models/auth.models';
import {
  DashboardCards,
  DashboardMetrics,
  DepartmentDashboard,
  OrganizationDashboard,
} from '../../../models/dashboard.models';
import { Department } from '../../../models/department.models';
import { TICKET_STATUS_VALUES } from '../../../models/ticket.models';
import { AuthService } from '../../../services/auth.service';
import { fakeUser } from '../../../services/testing/auth-fixtures';

export const MANAGER_ID = 31;
export const MANAGED_DEPARTMENT_ID = 4;

export function fakeDashboardAuth(roles: Role[], id = MANAGER_ID): Partial<AuthService> {
  const user = signal(fakeUser({ id, roles }));
  return {
    currentUser: user.asReadonly(),
    roles: signal<readonly Role[]>(roles).asReadonly(),
    hasAnyRole: (wanted: readonly Role[]) => wanted.some((role) => roles.includes(role)),
  };
}

export function fakeDepartment(overrides: Partial<Department> = {}): Department {
  return {
    id: MANAGED_DEPARTMENT_ID,
    name: 'הנהלת חשבונות',
    isActive: true,
    manager: { id: MANAGER_ID, employeeCode: 'E1031', firstName: 'רונית', lastName: 'לוי' },
    hrUser: null,
    activeUserCount: 8,
    handlerCount: 2,
    ...overrides,
  };
}

export function fakeCards(overrides: Partial<DashboardCards> = {}): DashboardCards {
  return {
    openCount: 12,
    newThisWeekCount: 5,
    overdueOpenCount: 2,
    openedInRangeCount: 30,
    resolvedInRangeCount: 20,
    averageResolutionHours: 28,
    slaCompliancePercent: 85,
    autoClassifiedPercent: 70.5,
    ...overrides,
  };
}

function metrics(cards: DashboardCards): DashboardMetrics {
  return {
    range: { from: '2026-10-19', to: '2026-11-17' },
    generatedAt: '2026-11-17T08:00:00Z',
    cards,
    byCategory: [
      { categoryId: 1, categoryName: 'תקלת מחשב', count: 4 },
      { categoryId: 2, categoryName: 'שכר ותלושים', count: 9 },
      { categoryId: null, categoryName: null, count: 1 },
    ],
    byStatus: TICKET_STATUS_VALUES.map((status, i) => ({ status, count: i + 1 })),
    trend: [
      { date: '2026-11-15', opened: 3, closed: 1 },
      { date: '2026-11-16', opened: 5, closed: 2 },
      { date: '2026-11-17', opened: 0, closed: 4 },
    ],
    oldestOpen: [
      {
        id: 1042,
        subject: 'המחשב לא נדלק',
        isSubjectExcerpt: false,
        requesterDepartmentName: 'פיתוח',
        assignedDepartmentName: 'מחשוב',
        status: 'InProgress',
        createdAt: '2026-10-01T08:00:00Z',
        dueAt: '2026-10-02T08:00:00Z',
        isOverdue: true,
      },
    ],
  };
}

export function fakeDepartmentDashboard(
  overrides: Partial<DepartmentDashboard> = {},
  cards: Partial<DashboardCards> = {},
): DepartmentDashboard {
  return {
    ...metrics(fakeCards(cards)),
    department: { id: MANAGED_DEPARTMENT_ID, name: 'הנהלת חשבונות', isActive: true },
    view: 'Requesters',
    ...overrides,
  };
}

export function fakeOrganizationDashboard(overrides: Partial<OrganizationDashboard> = {}): OrganizationDashboard {
  return {
    ...metrics(fakeCards()),
    departments: [
      { departmentId: 2, departmentName: 'מחשוב', isActive: true, openCount: 10, overdueOpenCount: 1, resolvedInRangeCount: 7, averageResolutionHours: 5 },
      { departmentId: 3, departmentName: 'משאבי אנוש', isActive: true, openCount: 4, overdueOpenCount: 3, resolvedInRangeCount: 9, averageResolutionHours: 30 },
      { departmentId: 4, departmentName: 'הנהלת חשבונות', isActive: false, openCount: 6, overdueOpenCount: 1, resolvedInRangeCount: 2, averageResolutionHours: null },
      { departmentId: null, departmentName: 'לא נותבו', isActive: true, openCount: 1, overdueOpenCount: 0, resolvedInRangeCount: 0, averageResolutionHours: null },
    ],
    ...overrides,
  };
}
