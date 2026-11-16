import { signal } from '@angular/core';

import { CurrentUser, Role } from '../../../models/auth.models';
import {
  StaffTicketDetails,
  StaffTicketHistory,
  StaffTicketListItem,
  StaffTimelineItem,
  TicketAction,
} from '../../../models/ticket.models';
import { AuthService } from '../../../services/auth.service';
import { fakeUser } from '../../../services/testing/auth-fixtures';

/** Test data only – not imported by application code. */

export const HANDLER_ID = 14;

export function fakeStaffItem(overrides: Partial<StaffTicketListItem> = {}): StaffTicketListItem {
  return {
    id: 152,
    subject: 'המדפסת לא מדפיסה',
    isSubjectExcerpt: false,
    requesterName: 'נועה ברקוביץ',
    requesterDepartmentName: 'פיתוח',
    categoryName: 'מחשוב',
    urgency: 'High',
    status: 'Assigned',
    handlerName: null,
    assignedDepartmentName: 'מערכות מידע',
    dueAt: new Date(Date.now() + 3 * 3_600_000 + 60_000).toISOString(),
    isOverdue: false,
    classificationSource: 'Ai',
    createdAt: '2026-11-16T08:00:00Z',
    updatedAt: '2026-11-16T08:00:00Z',
    rowVersion: 'AAAAAAAAB9E=',
    ...overrides,
  };
}

export const ALL_STAFF_ACTIONS: TicketAction[] = ['reply', 'internalNote', 'reclassify', 'assign', 'transferToDepartment'];

export function fakeStaffDetails(overrides: Partial<StaffTicketDetails> = {}): StaffTicketDetails {
  return {
    id: 152,
    subject: 'המדפסת לא מדפיסה',
    description: 'המדפסת בקומה 2 לא מדפיסה.\nניסיתי לכבות ולהדליק.',
    status: 'InProgress',
    requester: { id: 1, name: 'נועה ברקוביץ', departmentName: 'פיתוח', jobTitle: 'מפתחת' },
    category: { id: 3, name: 'מחשוב' },
    urgency: 'High',
    classificationSource: 'Ai',
    dueAt: new Date(Date.now() + 5 * 3_600_000).toISOString(),
    isOverdue: false,
    assignedTo: { id: HANDLER_ID, name: 'אלון ביטון' },
    assignedDepartment: { id: 10, name: 'מערכות מידע' },
    createdAt: '2026-11-16T08:00:00Z',
    updatedAt: '2026-11-16T08:30:00Z',
    resolvedAt: null,
    closedAt: null,
    rowVersion: 'AAAAAAAAB9E=',
    allowedActions: [...ALL_STAFF_ACTIONS],
    ...overrides,
  };
}

export function fakeStaffHistory(items?: StaffTimelineItem[], ticketId = 152): StaffTicketHistory {
  return {
    ticketId,
    items: items ?? [
      {
        kind: 'status',
        at: '2026-11-16T08:00:00Z',
        previousStatus: null,
        newStatus: 'New',
        changedByName: 'נועה ברקוביץ',
        reason: null,
      },
    ],
  };
}

/** A signed-in handler (only what the screens read: the id and the roles). */
export function fakeAuth(roles: Role[] = ['Employee', 'Handler']): Partial<AuthService> {
  const user = signal<CurrentUser | null>(fakeUser({ id: HANDLER_ID, roles }));
  return { currentUser: user.asReadonly(), roles: signal(roles).asReadonly() };
}
